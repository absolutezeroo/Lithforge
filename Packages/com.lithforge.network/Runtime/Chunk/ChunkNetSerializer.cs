using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Collections;
using Unity.Mathematics;

using ZstdSharp;

namespace Lithforge.Network.Chunk
{
    /// <summary>
    ///     Network-specific chunk serialization. Separate from <see cref="Storage.ChunkSerializer" />
    ///     which handles disk persistence. Uses palette compression + zstd (level 1) for full chunks,
    ///     and compact local-coordinate encoding for block change batches.
    /// </summary>
    public static class ChunkNetSerializer
    {
        /// <summary>
        /// Version byte written at the start of full chunk packets for forward compatibility.
        /// </summary>
        private const byte FullChunkVersion = 1;

        /// <summary>
        /// Zstd compression level (1 = fastest) for chunk data.
        /// </summary>
        private const int ZstdCompressionLevel = 1;

        /// <summary>Batch header flag: bit 0 = compressed with zstd.</summary>
        private const byte BatchFlagCompressed = 1;

        /// <summary>Batch header flag: bit 1 = single-entry shortcut format.</summary>
        private const byte BatchFlagSingleEntry = 2;

        /// <summary>
        ///     Threshold for compressing block change batches. Batches with fewer entries
        ///     than this are sent uncompressed because the zstd frame header overhead
        ///     would exceed any compression savings.
        /// </summary>
        private const int BatchCompressionThreshold = 20;

        /// <summary>Bytes per entry in a block change batch: 3 (local xyz) + 2 (StateId) = 5.</summary>
        private const int BytesPerBatchEntry = 5;
        /// <summary>
        ///     Magic bytes for full chunk network packets: "LFNC" (Lithforge Network Chunk).
        /// </summary>
        private static readonly byte[] s_fullChunkMagic =
        {
            (byte)'L',
            (byte)'F',
            (byte)'N',
            (byte)'C',
        };

        /// <summary>
        /// Thread-local reusable buffer for voxel palette index data during serialization.
        /// </summary>
        [ThreadStatic] private static byte[] s_voxelBuffer;

        /// <summary>
        /// Thread-local reusable buffer for light data during serialization.
        /// </summary>
        [ThreadStatic] private static byte[] s_lightBuffer;

        /// <summary>
        /// Thread-local reusable memory stream for building serialized payloads.
        /// </summary>
        [ThreadStatic] private static MemoryStream s_stream;

        /// <summary>
        /// Thread-local zstd compressor instance, lazily initialized.
        /// </summary>
        [ThreadStatic] private static Compressor s_compressor;

        /// <summary>
        /// Thread-local zstd decompressor instance, lazily initialized.
        /// </summary>
        [ThreadStatic] private static Decompressor s_decompressor;

        /// <summary>
        /// Thread-local reusable buffer for the palette lookup during deserialization.
        /// </summary>
        [ThreadStatic] private static ushort[] s_paletteBuffer;

        /// <summary>
        /// Returns the thread-local zstd compressor, creating it if needed.
        /// </summary>
        private static Compressor GetCompressor()
        {
            if (s_compressor == null)
            {
                s_compressor = new Compressor(ZstdCompressionLevel);
            }

            return s_compressor;
        }

        /// <summary>
        /// Returns the thread-local zstd decompressor, creating it if needed.
        /// </summary>
        private static Decompressor GetDecompressor()
        {
            if (s_decompressor == null)
            {
                s_decompressor = new Decompressor();
            }

            return s_decompressor;
        }

        /// <summary>
        /// Returns the thread-local memory stream, resetting it for reuse.
        /// </summary>
        private static MemoryStream GetStream()
        {
            if (s_stream == null)
            {
                s_stream = new MemoryStream(128 * 1024);
            }

            s_stream.SetLength(0);
            s_stream.Position = 0;

            return s_stream;
        }

        /// <summary>
        ///     Disposes thread-local compressor/decompressor and stream resources.
        ///     Call from test teardown or thread shutdown when explicit cleanup is needed.
        ///     Not required in production (thread-pool threads live for the process lifetime).
        /// </summary>
        public static void DisposeThreadLocalResources()
        {
            s_compressor?.Dispose();
            s_compressor = null;
            s_decompressor?.Dispose();
            s_decompressor = null;
            s_stream?.Dispose();
            s_stream = null;
            s_voxelBuffer = null;
            s_lightBuffer = null;
            s_paletteBuffer = null;
        }

        /// <summary>
        ///     Serializes a full chunk for initial network transmission.
        ///     Format: magic(4) + version(1) + zstd[ paletteCount(u16) + palette(N*u16) +
        ///     voxelDataLen(i32) + voxelData(palette indices) +
        ///     lightDataLen(i32) + lightData(nibble-packed bytes) ].
        ///     Single-valued sections store paletteCount=1 + value + voxelDataLen=0.
        /// </summary>
        public static byte[] SerializeFullChunk(NativeArray<StateId> chunkData, NativeArray<byte> lightData)
        {
            MemoryStream stream = GetStream();
            BinaryWriter writer = new(stream, Encoding.UTF8, true);

            // Build palette
            Dictionary<ushort, ushort> paletteMap = new();
            List<ushort> paletteList = new();

            for (int i = 0; i < chunkData.Length; i++)
            {
                ushort val = chunkData[i].Value;

                if (!paletteMap.ContainsKey(val))
                {
                    paletteMap[val] = (ushort)paletteList.Count;
                    paletteList.Add(val);
                }
            }

            bool isSingleValued = paletteList.Count == 1;

            // Write palette
            writer.Write((ushort)paletteList.Count);

            for (int i = 0; i < paletteList.Count; i++)
            {
                writer.Write(paletteList[i]);
            }

            if (isSingleValued)
            {
                // No voxel index data needed — entire chunk is one block type
                writer.Write(0);
            }
            else
            {
                // Write palette indices as raw bytes
                if (s_voxelBuffer == null || s_voxelBuffer.Length < chunkData.Length * 2)
                {
                    s_voxelBuffer = new byte[ChunkConstants.Volume * 2];
                }

                for (int i = 0; i < chunkData.Length; i++)
                {
                    ushort idx = paletteMap[chunkData[i].Value];
                    s_voxelBuffer[i * 2] = (byte)(idx & 0xFF);
                    s_voxelBuffer[i * 2 + 1] = (byte)(idx >> 8);
                }

                int voxelByteCount = chunkData.Length * 2;
                writer.Write(voxelByteCount);
                writer.Write(s_voxelBuffer, 0, voxelByteCount);
            }

            // Write light data
            if (lightData is
                {
                    IsCreated: true,
                    Length: > 0,
                })
            {
                if (s_lightBuffer == null || s_lightBuffer.Length < lightData.Length)
                {
                    s_lightBuffer = new byte[ChunkConstants.Volume];
                }

                lightData.CopyTo(s_lightBuffer);
                writer.Write(lightData.Length);
                writer.Write(s_lightBuffer, 0, lightData.Length);
            }
            else
            {
                writer.Write(0);
            }

            writer.Flush();

            // Wrap entire payload in zstd compression with magic+version header
            byte[] uncompressedPayload = stream.ToArray();
            Compressor compressor = GetCompressor();
            byte[] compressedPayload = compressor.Wrap(uncompressedPayload).ToArray();

            byte[] result = new byte[s_fullChunkMagic.Length + 1 + compressedPayload.Length];
            Buffer.BlockCopy(s_fullChunkMagic, 0, result, 0, s_fullChunkMagic.Length);
            result[s_fullChunkMagic.Length] = FullChunkVersion;
            Buffer.BlockCopy(compressedPayload, 0, result, s_fullChunkMagic.Length + 1, compressedPayload.Length);

            writer.Dispose();
            return result;
        }

        /// <summary>
        ///     Deserializes a full chunk received from the network.
        /// </summary>
        /// <returns>True if deserialization succeeded, false on invalid data.</returns>
        public static bool DeserializeFullChunk(byte[] data, NativeArray<StateId> target, NativeArray<byte> lightTarget)
        {
            if (data == null || data.Length < s_fullChunkMagic.Length + 1)
            {
                return false;
            }

            // Verify magic
            for (int i = 0; i < s_fullChunkMagic.Length; i++)
            {
                if (data[i] != s_fullChunkMagic[i])
                {
                    return false;
                }
            }

            byte version = data[s_fullChunkMagic.Length];

            if (version != FullChunkVersion)
            {
                return false;
            }

            // Decompress zstd payload
            int compressedOffset = s_fullChunkMagic.Length + 1;
            int compressedLength = data.Length - compressedOffset;
            Decompressor decompressor = GetDecompressor();
            byte[] decompressed = decompressor.Unwrap(
                new ReadOnlySpan<byte>(data, compressedOffset, compressedLength)).ToArray();

            using (MemoryStream ms = new(decompressed))
            using (BinaryReader reader = new(ms))
            {
                // Read palette (reuse ThreadStatic buffer)
                ushort paletteCount = reader.ReadUInt16();

                if (s_paletteBuffer == null || s_paletteBuffer.Length < paletteCount)
                {
                    s_paletteBuffer = new ushort[math.max(paletteCount, 256)];
                }

                for (int i = 0; i < paletteCount; i++)
                {
                    s_paletteBuffer[i] = reader.ReadUInt16();
                }

                // Read voxel data
                int voxelByteCount = reader.ReadInt32();

                if (voxelByteCount == 0)
                {
                    // Single-valued chunk: fill target with the sole palette entry
                    if (paletteCount != 1)
                    {
                        return false;
                    }

                    StateId fillState = new(s_paletteBuffer[0]);

                    for (int i = 0; i < target.Length; i++)
                    {
                        target[i] = fillState;
                    }
                }
                else
                {
                    int expectedBytes = target.Length * 2;

                    if (voxelByteCount != expectedBytes)
                    {
                        return false;
                    }

                    // Reuse ThreadStatic voxel buffer
                    if (s_voxelBuffer == null || s_voxelBuffer.Length < voxelByteCount)
                    {
                        s_voxelBuffer = new byte[ChunkConstants.Volume * 2];
                    }

                    int bytesRead = reader.Read(s_voxelBuffer, 0, voxelByteCount);

                    if (bytesRead < voxelByteCount)
                    {
                        return false;
                    }

                    for (int i = 0; i < target.Length; i++)
                    {
                        ushort paletteIdx = (ushort)(s_voxelBuffer[i * 2] | s_voxelBuffer[i * 2 + 1] << 8);

                        if (paletteIdx >= paletteCount)
                        {
                            return false;
                        }

                        target[i] = new StateId(s_paletteBuffer[paletteIdx]);
                    }
                }

                // Read light data
                int lightByteCount = reader.ReadInt32();

                if (lightByteCount > 0 && lightTarget.IsCreated)
                {
                    if (lightByteCount < lightTarget.Length)
                    {
                        return false;
                    }

                    // CopyFrom requires exact length match, so read into an exact-sized buffer
                    if (s_lightBuffer == null || s_lightBuffer.Length != lightTarget.Length)
                    {
                        s_lightBuffer = new byte[lightTarget.Length];
                    }

                    int lightBytesRead = reader.Read(s_lightBuffer, 0, lightTarget.Length);

                    if (lightBytesRead < lightTarget.Length)
                    {
                        return false;
                    }

                    lightTarget.CopyFrom(s_lightBuffer);

                    // Skip any extra bytes beyond what we need
                    if (lightByteCount > lightTarget.Length)
                    {
                        reader.ReadBytes(lightByteCount - lightTarget.Length);
                    }
                }
            }

            return true;
        }

        /// <summary>
        ///     Serializes a batch of block changes for a single chunk.
        ///     Format: header(1) + chunkCoord(12) + count(u16) + entries(N * 5 bytes).
        ///     Each entry is: localX(1) + localY(1) + localZ(1) + stateId(2).
        ///     If count >= 20, the entry data is zstd-compressed.
        ///     Single-change shortcut: header(1) + chunkCoord(12) + localXYZ(3) + stateId(2) = 18 bytes.
        /// </summary>
        public static byte[] SerializeBlockChangeBatch(int3 chunkCoord, List<BlockChangeEntry> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return Array.Empty<byte>();
            }

            if (changes.Count == 1)
            {
                // Single-change shortcut: no compression, no count field
                byte[] result = new byte[1 + 12 + 3 + 2]; // 18 bytes
                result[0] = BatchFlagSingleEntry;         // header: single-entry shortcut
                WriteInt3(result, 1, chunkCoord);
                BlockChangeEntry entry = changes[0];
                int3 local = WorldToLocal(entry.Position, chunkCoord);
                result[13] = (byte)local.x;
                result[14] = (byte)local.y;
                result[15] = (byte)local.z;
                result[16] = (byte)(entry.NewState.Value & 0xFF);
                result[17] = (byte)(entry.NewState.Value >> 8);
                return result;
            }

            // Multi-change path
            int entryDataSize = changes.Count * BytesPerBatchEntry;
            byte[] entryData = new byte[entryDataSize];

            for (int i = 0; i < changes.Count; i++)
            {
                BlockChangeEntry change = changes[i];
                int3 local = WorldToLocal(change.Position, chunkCoord);
                int offset = i * BytesPerBatchEntry;
                entryData[offset] = (byte)local.x;
                entryData[offset + 1] = (byte)local.y;
                entryData[offset + 2] = (byte)local.z;
                entryData[offset + 3] = (byte)(change.NewState.Value & 0xFF);
                entryData[offset + 4] = (byte)(change.NewState.Value >> 8);
            }

            bool compress = changes.Count >= BatchCompressionThreshold;
            byte header = compress ? BatchFlagCompressed : (byte)0;

            byte[] payload;

            if (compress)
            {
                Compressor compressor = GetCompressor();
                payload = compressor.Wrap(entryData).ToArray();
            }
            else
            {
                payload = entryData;
            }

            // header(1) + chunkCoord(12) + count(2) + payload
            byte[] batchResult = new byte[1 + 12 + 2 + payload.Length];
            batchResult[0] = header;
            WriteInt3(batchResult, 1, chunkCoord);
            batchResult[13] = (byte)(changes.Count & 0xFF);
            batchResult[14] = (byte)(changes.Count >> 8 & 0xFF);
            Buffer.BlockCopy(payload, 0, batchResult, 15, payload.Length);
            return batchResult;
        }

        /// <summary>
        ///     Deserializes a block change batch. Returns the chunk coordinate and list of changes
        ///     with world-space positions reconstructed from the chunk coordinate and local offsets.
        /// </summary>
        /// <returns>True if deserialization succeeded.</returns>
        public static bool DeserializeBlockChangeBatch(
            byte[] data,
            out int3 chunkCoord,
            out List<BlockChangeEntry> changes)
        {
            chunkCoord = int3.zero;
            changes = null;

            if (data == null || data.Length < 18) // minimum: header(1) + chunk(12) + local(3) + state(2)
            {
                return false;
            }

            byte header = data[0];
            chunkCoord = ReadInt3(data, 1);

            // Detect single-change shortcut via header flag
            if ((header & BatchFlagSingleEntry) != 0)
            {
                changes = new List<BlockChangeEntry>(1);
                int3 local = new(data[13], data[14], data[15]);
                ushort stateVal = (ushort)(data[16] | data[17] << 8);
                changes.Add(new BlockChangeEntry
                {
                    Position = LocalToWorld(local, chunkCoord), NewState = new StateId(stateVal),
                });
                return true;
            }

            // Multi-change path
            if (data.Length < 15) // header(1) + chunk(12) + count(2)
            {
                return false;
            }

            ushort count = (ushort)(data[13] | data[14] << 8);
            bool compressed = (header & BatchFlagCompressed) != 0;

            byte[] entryData;

            if (compressed)
            {
                int compressedLength = data.Length - 15;
                Decompressor decompressor = GetDecompressor();
                entryData = decompressor.Unwrap(
                    new ReadOnlySpan<byte>(data, 15, compressedLength)).ToArray();
            }
            else
            {
                int rawLength = data.Length - 15;
                entryData = new byte[rawLength];
                Buffer.BlockCopy(data, 15, entryData, 0, rawLength);
            }

            int expectedSize = count * BytesPerBatchEntry;

            if (entryData.Length < expectedSize)
            {
                return false;
            }

            changes = new List<BlockChangeEntry>(count);

            for (int i = 0; i < count; i++)
            {
                int offset = i * BytesPerBatchEntry;
                int3 local = new(entryData[offset], entryData[offset + 1], entryData[offset + 2]);
                ushort stateVal = (ushort)(entryData[offset + 3] | entryData[offset + 4] << 8);
                changes.Add(new BlockChangeEntry
                {
                    Position = LocalToWorld(local, chunkCoord), NewState = new StateId(stateVal),
                });
            }

            return true;
        }

        /// <summary>
        /// Converts a world-space coordinate to a chunk-local 0-31 coordinate.
        /// </summary>
        private static int3 WorldToLocal(int3 worldCoord, int3 chunkCoord)
        {
            return new int3(
                worldCoord.x - chunkCoord.x * ChunkConstants.Size,
                worldCoord.y - chunkCoord.y * ChunkConstants.Size,
                worldCoord.z - chunkCoord.z * ChunkConstants.Size);
        }

        /// <summary>
        /// Converts a chunk-local coordinate back to world-space.
        /// </summary>
        private static int3 LocalToWorld(int3 local, int3 chunkCoord)
        {
            return new int3(
                local.x + chunkCoord.x * ChunkConstants.Size,
                local.y + chunkCoord.y * ChunkConstants.Size,
                local.z + chunkCoord.z * ChunkConstants.Size);
        }

        /// <summary>
        /// Writes an int3 as 12 little-endian bytes to the buffer.
        /// </summary>
        private static void WriteInt3(byte[] buffer, int offset, int3 value)
        {
            buffer[offset] = (byte)(value.x & 0xFF);
            buffer[offset + 1] = (byte)(value.x >> 8 & 0xFF);
            buffer[offset + 2] = (byte)(value.x >> 16 & 0xFF);
            buffer[offset + 3] = (byte)(value.x >> 24 & 0xFF);
            buffer[offset + 4] = (byte)(value.y & 0xFF);
            buffer[offset + 5] = (byte)(value.y >> 8 & 0xFF);
            buffer[offset + 6] = (byte)(value.y >> 16 & 0xFF);
            buffer[offset + 7] = (byte)(value.y >> 24 & 0xFF);
            buffer[offset + 8] = (byte)(value.z & 0xFF);
            buffer[offset + 9] = (byte)(value.z >> 8 & 0xFF);
            buffer[offset + 10] = (byte)(value.z >> 16 & 0xFF);
            buffer[offset + 11] = (byte)(value.z >> 24 & 0xFF);
        }

        /// <summary>
        /// Reads an int3 from 12 little-endian bytes in the buffer.
        /// </summary>
        private static int3 ReadInt3(byte[] buffer, int offset)
        {
            int x = buffer[offset] | buffer[offset + 1] << 8 |
                    buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
            int y = buffer[offset + 4] | buffer[offset + 5] << 8 |
                    buffer[offset + 6] << 16 | buffer[offset + 7] << 24;
            int z = buffer[offset + 8] | buffer[offset + 9] << 8 |
                    buffer[offset + 10] << 16 | buffer[offset + 11] << 24;
            return new int3(x, y, z);
        }
    }
}
