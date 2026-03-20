using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using K4os.Compression.LZ4;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;

using Unity.Collections;

using ZstdSharp;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Serializes and deserializes chunk data using a binary format with palette compression,
    ///     Zstd compression, and CRC-32 integrity checking. Format version 5
    ///     (backward-compatible with v3 and v4 for deserialization). Thread-safe via ThreadStatic scratch buffers.
    /// </summary>
    public static class ChunkSerializer
    {
        /// <summary>Current binary format version (6 = Zstd + InhabitedTime).</summary>
        private const byte Version = 6;

        /// <summary>Zstd compression level (1 = fastest, best for real-time saves).</summary>
        private const int ZstdCompressionLevel = 1;

        /// <summary>Magic bytes identifying the file as a Lithforge chunk ("LFCH").</summary>
        private static readonly byte[] s_magic =
        {
            (byte)'L',
            (byte)'F',
            (byte)'C',
            (byte)'H',
        };

        /// <summary>Per-thread scratch buffer for voxel palette index encoding.</summary>
        [ThreadStatic] private static byte[] s_voxelBuffer;

        /// <summary>Per-thread scratch buffer for light data encoding.</summary>
        [ThreadStatic] private static byte[] s_lightBuffer;

        /// <summary>Per-thread reusable MemoryStream to avoid allocation per serialize call.</summary>
        [ThreadStatic] private static MemoryStream s_stream;

        /// <summary>Per-thread scratch buffer for compressed/decompressed I/O.</summary>
        [ThreadStatic] private static byte[] s_compressBuffer;

        /// <summary>Per-thread Zstd compressor context, lazily initialized.</summary>
        [ThreadStatic] private static Compressor s_compressor;

        /// <summary>Per-thread Zstd decompressor context, lazily initialized.</summary>
        [ThreadStatic] private static Decompressor s_decompressor;

        /// <summary>Returns the thread-local Zstd compressor, creating it if needed.</summary>
        private static Compressor GetCompressor()
        {
            if (s_compressor == null)
            {
                s_compressor = new Compressor(ZstdCompressionLevel);
            }

            return s_compressor;
        }

        /// <summary>Returns the thread-local Zstd decompressor, creating it if needed.</summary>
        private static Decompressor GetDecompressor()
        {
            if (s_decompressor == null)
            {
                s_decompressor = new Decompressor();
            }

            return s_decompressor;
        }

        /// <summary>Ensures the compress buffer is large enough for the given size.</summary>
        private static void EnsureCompressBuffer(int requiredSize)
        {
            if (s_compressBuffer == null || s_compressBuffer.Length < requiredSize)
            {
                s_compressBuffer = new byte[Math.Max(requiredSize, ChunkConstants.Volume * 2)];
            }
        }

        /// <summary>
        ///     Serializes chunk data from NativeArrays (main-thread path).
        ///     Palette-compresses voxel data, Zstd-compresses both voxel and light,
        ///     includes block entities and inhabited time, and appends a CRC-32 checksum.
        /// </summary>
        public static byte[] Serialize(
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities = null,
            float inhabitedTime = 0f)
        {
            if (s_stream == null)
            {
                s_stream = new MemoryStream(128 * 1024);
            }

            s_stream.SetLength(0);
            s_stream.Position = 0;

            BinaryWriter writer = new(s_stream, Encoding.UTF8, true);

            // Header
            writer.Write(s_magic);
            writer.Write(Version);

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

            // Write palette
            writer.Write((ushort)paletteList.Count);

            for (int i = 0; i < paletteList.Count; i++)
            {
                writer.Write(paletteList[i]);
            }

            // Compress voxel data (palette indices) with Zstd
            int voxelByteCount = chunkData.Length * 2;

            if (s_voxelBuffer == null || s_voxelBuffer.Length < voxelByteCount)
            {
                s_voxelBuffer = new byte[ChunkConstants.Volume * 2];
            }

            for (int i = 0; i < chunkData.Length; i++)
            {
                ushort idx = paletteMap[chunkData[i].Value];
                s_voxelBuffer[i * 2] = (byte)(idx & 0xFF);
                s_voxelBuffer[i * 2 + 1] = (byte)(idx >> 8);
            }

            Compressor compressor = GetCompressor();
            int voxelBound = Compressor.GetCompressBound(voxelByteCount);
            EnsureCompressBuffer(voxelBound);
            int voxelCompLen = compressor.Wrap(
                new ReadOnlySpan<byte>(s_voxelBuffer, 0, voxelByteCount),
                new Span<byte>(s_compressBuffer, 0, s_compressBuffer.Length));
            writer.Write(voxelByteCount);
            writer.Write(voxelCompLen);
            writer.Write(s_compressBuffer, 0, voxelCompLen);

            // Compress light data with Zstd
            if (lightData is
                {
                    IsCreated: true,
                    Length: > 0,
                })
            {
                int lightByteCount = lightData.Length;

                if (s_lightBuffer == null || s_lightBuffer.Length < lightByteCount)
                {
                    s_lightBuffer = new byte[ChunkConstants.Volume];
                }

                lightData.CopyTo(s_lightBuffer);

                int lightBound = Compressor.GetCompressBound(lightByteCount);
                EnsureCompressBuffer(lightBound);
                int lightCompLen = compressor.Wrap(
                    new ReadOnlySpan<byte>(s_lightBuffer, 0, lightByteCount),
                    new Span<byte>(s_compressBuffer, 0, s_compressBuffer.Length));
                writer.Write(lightByteCount);
                writer.Write(lightCompLen);
                writer.Write(s_compressBuffer, 0, lightCompLen);
            }
            else
            {
                writer.Write(0);
                writer.Write(0);
            }

            // Block entity section
            WriteBlockEntities(writer, blockEntities);

            // InhabitedTime (v6+)
            writer.Write(inhabitedTime);

            // Append CRC32 checksum of all preceding bytes
            writer.Flush();
            byte[] streamBuf = s_stream.GetBuffer();
            int payloadLen = (int)s_stream.Position;
            uint crc = Crc32.Compute(streamBuf, 0, payloadLen);

            writer.Write(crc);

            int resultLen = (int)s_stream.Position;
            byte[] result = new byte[resultLen];
            Buffer.BlockCopy(s_stream.GetBuffer(), 0, result, 0, resultLen);
            writer.Dispose();

            return result;
        }

        /// <summary>
        ///     Serializes chunk data from raw byte[] snapshots (used by I/O worker thread).
        ///     voxelSnapshot contains StateId values as little-endian ushorts (2 bytes each).
        /// </summary>
        public static byte[] Serialize(
            byte[] voxelSnapshot,
            int voxelCount,
            byte[] lightSnapshot,
            Dictionary<int, IBlockEntity> blockEntities = null,
            float inhabitedTime = 0f)
        {
            if (s_stream == null)
            {
                s_stream = new MemoryStream(128 * 1024);
            }

            s_stream.SetLength(0);
            s_stream.Position = 0;

            BinaryWriter writer = new(s_stream, Encoding.UTF8, true);

            // Header
            writer.Write(s_magic);
            writer.Write(Version);

            // Build palette from raw voxel bytes
            Dictionary<ushort, ushort> paletteMap = new();
            List<ushort> paletteList = new();

            for (int i = 0; i < voxelCount; i++)
            {
                ushort val = (ushort)(voxelSnapshot[i * 2] | voxelSnapshot[i * 2 + 1] << 8);

                if (!paletteMap.ContainsKey(val))
                {
                    paletteMap[val] = (ushort)paletteList.Count;
                    paletteList.Add(val);
                }
            }

            // Write palette
            writer.Write((ushort)paletteList.Count);

            for (int i = 0; i < paletteList.Count; i++)
            {
                writer.Write(paletteList[i]);
            }

            // Compress voxel data (palette indices) with Zstd
            int voxelByteCount = voxelCount * 2;

            if (s_voxelBuffer == null || s_voxelBuffer.Length < voxelByteCount)
            {
                s_voxelBuffer = new byte[ChunkConstants.Volume * 2];
            }

            for (int i = 0; i < voxelCount; i++)
            {
                ushort val = (ushort)(voxelSnapshot[i * 2] | voxelSnapshot[i * 2 + 1] << 8);
                ushort idx = paletteMap[val];
                s_voxelBuffer[i * 2] = (byte)(idx & 0xFF);
                s_voxelBuffer[i * 2 + 1] = (byte)(idx >> 8);
            }

            Compressor compressor = GetCompressor();
            int voxelBound = Compressor.GetCompressBound(voxelByteCount);
            EnsureCompressBuffer(voxelBound);
            int voxelCompLen = compressor.Wrap(
                new ReadOnlySpan<byte>(s_voxelBuffer, 0, voxelByteCount),
                new Span<byte>(s_compressBuffer, 0, s_compressBuffer.Length));
            writer.Write(voxelByteCount);
            writer.Write(voxelCompLen);
            writer.Write(s_compressBuffer, 0, voxelCompLen);

            // Compress light data with Zstd
            if (lightSnapshot is
                {
                    Length: > 0,
                })
            {
                int lightByteCount = lightSnapshot.Length;
                int lightBound = Compressor.GetCompressBound(lightByteCount);
                EnsureCompressBuffer(lightBound);
                Compressor lightCompressor = GetCompressor();
                int lightCompLen = lightCompressor.Wrap(
                    new ReadOnlySpan<byte>(lightSnapshot, 0, lightByteCount),
                    new Span<byte>(s_compressBuffer, 0, s_compressBuffer.Length));
                writer.Write(lightByteCount);
                writer.Write(lightCompLen);
                writer.Write(s_compressBuffer, 0, lightCompLen);
            }
            else
            {
                writer.Write(0);
                writer.Write(0);
            }

            // Block entity section
            WriteBlockEntities(writer, blockEntities);

            // InhabitedTime (v6+)
            writer.Write(inhabitedTime);

            // Append CRC32 checksum of all preceding bytes
            writer.Flush();
            byte[] streamBuf = s_stream.GetBuffer();
            int payloadLen = (int)s_stream.Position;
            uint crc = Crc32.Compute(streamBuf, 0, payloadLen);

            writer.Write(crc);

            int resultLen = (int)s_stream.Position;
            byte[] result = new byte[resultLen];
            Buffer.BlockCopy(s_stream.GetBuffer(), 0, result, 0, resultLen);
            writer.Dispose();

            return result;
        }

        /// <summary>Writes the block entity section: count, then per-entity (flatIndex, typeId, length-prefixed payload).</summary>
        private static void WriteBlockEntities(BinaryWriter writer, Dictionary<int, IBlockEntity> blockEntities)
        {
            if (blockEntities is
                {
                    Count: > 0,
                })
            {
                writer.Write(blockEntities.Count);

                foreach (KeyValuePair<int, IBlockEntity> kvp in blockEntities)
                {
                    writer.Write(kvp.Key);          // flat index
                    writer.Write(kvp.Value.TypeId); // type ID string

                    // Length-prefixed entity payload
                    using (MemoryStream entityMs = new())
                    using (BinaryWriter entityWriter = new(entityMs))
                    {
                        kvp.Value.Serialize(entityWriter);
                        entityWriter.Flush();
                        byte[] entityData = entityMs.ToArray();
                        writer.Write(entityData.Length);
                        writer.Write(entityData);
                    }
                }
            }
            else
            {
                writer.Write(0);
            }
        }

        /// <summary>
        ///     Deserializes chunk data from a byte array into pre-allocated NativeArrays.
        ///     Verifies CRC-32 checksum, magic bytes, and version before reading data.
        ///     Supports format versions 3 (LZ4Pickler), 4 (LZ4Codec), 5 (Zstd), and 6 (Zstd + InhabitedTime).
        ///     Returns false if the data is corrupted, truncated, or version-mismatched.
        /// </summary>
        public static bool Deserialize(byte[] data,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry = null)
        {
            return Deserialize(data, chunkData, lightData, out blockEntities, out float _, blockEntityRegistry);
        }

        /// <summary>
        ///     Deserializes chunk data with InhabitedTime output. Supports v3-v6.
        ///     Versions prior to 6 return inhabitedTime = 0f.
        /// </summary>
        public static bool Deserialize(byte[] data,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            out float inhabitedTime,
            BlockEntityRegistry blockEntityRegistry = null)
        {
            blockEntities = null;
            inhabitedTime = 0f;

            if (data is null || data.Length < 9)
            {
                return false;
            }

            if (data.Length < 13)
            {
                return false;
            }

            int payloadLength = data.Length - 4;
            uint storedCrc = (uint)(data[payloadLength] |
                                    data[payloadLength + 1] << 8 |
                                    data[payloadLength + 2] << 16 |
                                    data[payloadLength + 3] << 24);
            uint computedCrc = Crc32.Compute(data, 0, payloadLength);

            if (storedCrc != computedCrc)
            {
                return false;
            }

            using (MemoryStream ms = new(data, 0, payloadLength))
            using (BinaryReader reader = new(ms))
            {
                byte[] magic = reader.ReadBytes(4);

                if (magic.Length != 4 ||
                    magic[0] != s_magic[0] || magic[1] != s_magic[1] ||
                    magic[2] != s_magic[2] || magic[3] != s_magic[3])
                {
                    return false;
                }

                byte version = reader.ReadByte();

                if (version == 3)
                {
                    return DeserializeV3(reader, ms, chunkData, lightData, out blockEntities, blockEntityRegistry);
                }

                if (version == 4)
                {
                    return DeserializeV4(reader, ms, chunkData, lightData, out blockEntities, blockEntityRegistry);
                }

                if (version == 5)
                {
                    return DeserializeV5(reader, ms, chunkData, lightData, out blockEntities, blockEntityRegistry);
                }

                if (version != 6)
                {
                    return false;
                }

                return DeserializeV6(reader, ms, chunkData, lightData, out blockEntities, out inhabitedTime, blockEntityRegistry);
            }
        }

        /// <summary>
        ///     Deserializes version 4 format (LZ4Codec). Kept for backward compatibility
        ///     with existing save files created before the Zstd migration.
        /// </summary>
        private static bool DeserializeV4(
            BinaryReader reader,
            MemoryStream ms,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry)
        {
            blockEntities = null;

            // Read palette
            ushort paletteCount = reader.ReadUInt16();
            ushort[] palette = new ushort[paletteCount];

            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = reader.ReadUInt16();
            }

            // Decompress voxel data with LZ4Codec
            int uncompVoxelLen = reader.ReadInt32();
            int compVoxelLen = reader.ReadInt32();

            EnsureCompressBuffer(compVoxelLen);
            reader.Read(s_compressBuffer, 0, compVoxelLen);

            if (s_voxelBuffer == null || s_voxelBuffer.Length < uncompVoxelLen)
            {
                s_voxelBuffer = new byte[Math.Max(uncompVoxelLen, ChunkConstants.Volume * 2)];
            }

            LZ4Codec.Decode(
                s_compressBuffer, 0, compVoxelLen,
                s_voxelBuffer, 0, uncompVoxelLen);

            int expectedVoxelBytes = chunkData.Length * 2;

            if (uncompVoxelLen < expectedVoxelBytes)
            {
                return false;
            }

            for (int i = 0; i < chunkData.Length; i++)
            {
                ushort paletteIdx = (ushort)(s_voxelBuffer[i * 2] | s_voxelBuffer[i * 2 + 1] << 8);

                if (paletteIdx >= paletteCount)
                {
                    return false;
                }

                chunkData[i] = new StateId(palette[paletteIdx]);
            }

            // Decompress light data with LZ4Codec
            int uncompLightLen = reader.ReadInt32();
            int compLightLen = reader.ReadInt32();

            if (uncompLightLen > 0 && compLightLen > 0 && lightData.IsCreated)
            {
                EnsureCompressBuffer(compLightLen);
                reader.Read(s_compressBuffer, 0, compLightLen);

                if (s_lightBuffer == null || s_lightBuffer.Length < uncompLightLen)
                {
                    s_lightBuffer = new byte[Math.Max(uncompLightLen, ChunkConstants.Volume)];
                }

                LZ4Codec.Decode(
                    s_compressBuffer, 0, compLightLen,
                    s_lightBuffer, 0, uncompLightLen);

                if (uncompLightLen < lightData.Length)
                {
                    return false;
                }

                lightData.CopyFrom(s_lightBuffer);
            }

            // Block entity section
            ReadBlockEntities(reader, ms, out blockEntities, blockEntityRegistry);

            return true;
        }

        /// <summary>
        ///     Deserializes version 5 format (Zstd compression). Current format.
        ///     Wire format is identical to v4 except Zstd replaces LZ4 for the
        ///     voxel and light compressed blocks.
        /// </summary>
        private static bool DeserializeV5(
            BinaryReader reader,
            MemoryStream ms,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry)
        {
            blockEntities = null;

            // Read palette
            ushort paletteCount = reader.ReadUInt16();
            ushort[] palette = new ushort[paletteCount];

            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = reader.ReadUInt16();
            }

            Decompressor decompressor = GetDecompressor();

            // Decompress voxel data with Zstd
            int uncompVoxelLen = reader.ReadInt32();
            int compVoxelLen = reader.ReadInt32();

            EnsureCompressBuffer(compVoxelLen);
            reader.Read(s_compressBuffer, 0, compVoxelLen);

            if (s_voxelBuffer == null || s_voxelBuffer.Length < uncompVoxelLen)
            {
                s_voxelBuffer = new byte[Math.Max(uncompVoxelLen, ChunkConstants.Volume * 2)];
            }

            decompressor.Unwrap(
                new ReadOnlySpan<byte>(s_compressBuffer, 0, compVoxelLen),
                new Span<byte>(s_voxelBuffer, 0, uncompVoxelLen));

            int expectedVoxelBytes = chunkData.Length * 2;

            if (uncompVoxelLen < expectedVoxelBytes)
            {
                return false;
            }

            for (int i = 0; i < chunkData.Length; i++)
            {
                ushort paletteIdx = (ushort)(s_voxelBuffer[i * 2] | s_voxelBuffer[i * 2 + 1] << 8);

                if (paletteIdx >= paletteCount)
                {
                    return false;
                }

                chunkData[i] = new StateId(palette[paletteIdx]);
            }

            // Decompress light data with Zstd
            int uncompLightLen = reader.ReadInt32();
            int compLightLen = reader.ReadInt32();

            if (uncompLightLen > 0 && compLightLen > 0 && lightData.IsCreated)
            {
                EnsureCompressBuffer(compLightLen);
                reader.Read(s_compressBuffer, 0, compLightLen);

                if (s_lightBuffer == null || s_lightBuffer.Length < uncompLightLen)
                {
                    s_lightBuffer = new byte[Math.Max(uncompLightLen, ChunkConstants.Volume)];
                }

                decompressor.Unwrap(
                    new ReadOnlySpan<byte>(s_compressBuffer, 0, compLightLen),
                    new Span<byte>(s_lightBuffer, 0, uncompLightLen));

                if (uncompLightLen < lightData.Length)
                {
                    return false;
                }

                lightData.CopyFrom(s_lightBuffer);
            }

            // Block entity section
            ReadBlockEntities(reader, ms, out blockEntities, blockEntityRegistry);

            return true;
        }

        /// <summary>
        ///     Deserializes version 6 format (Zstd + InhabitedTime). Same wire format as v5
        ///     with a float32 inhabitedTime appended after the block entity section.
        /// </summary>
        private static bool DeserializeV6(
            BinaryReader reader,
            MemoryStream ms,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            out float inhabitedTime,
            BlockEntityRegistry blockEntityRegistry)
        {
            inhabitedTime = 0f;

            if (!DeserializeV5(reader, ms, chunkData, lightData, out blockEntities, blockEntityRegistry))
            {
                return false;
            }

            // Read inhabitedTime if bytes remain (before CRC, which has already been stripped)
            if (ms.Position + 4 <= ms.Length)
            {
                inhabitedTime = reader.ReadSingle();
            }

            return true;
        }

        /// <summary>
        ///     Deserializes version 3 format (LZ4Pickler). Kept for backward compatibility
        ///     with existing save files.
        /// </summary>
        private static bool DeserializeV3(
            BinaryReader reader,
            MemoryStream ms,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry)
        {
            blockEntities = null;

            // Read palette
            ushort paletteCount = reader.ReadUInt16();
            ushort[] palette = new ushort[paletteCount];

            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = reader.ReadUInt16();
            }

            // Decompress voxel data with LZ4Pickler (v3 format)
            int compressedVoxelLen = reader.ReadInt32();
            byte[] compressedVoxels = reader.ReadBytes(compressedVoxelLen);
            byte[] voxelRaw = LZ4Pickler.Unpickle(compressedVoxels);

            int expectedVoxelBytes = chunkData.Length * 2;

            if (voxelRaw.Length < expectedVoxelBytes)
            {
                return false;
            }

            for (int i = 0; i < chunkData.Length; i++)
            {
                ushort paletteIdx = (ushort)(voxelRaw[i * 2] | voxelRaw[i * 2 + 1] << 8);

                if (paletteIdx >= paletteCount)
                {
                    return false;
                }

                chunkData[i] = new StateId(palette[paletteIdx]);
            }

            // Decompress light data with LZ4Pickler (v3 format)
            int compressedLightLen = reader.ReadInt32();

            if (compressedLightLen > 0 && lightData.IsCreated)
            {
                byte[] compressedLight = reader.ReadBytes(compressedLightLen);
                byte[] lightRaw = LZ4Pickler.Unpickle(compressedLight);

                if (lightRaw.Length < lightData.Length)
                {
                    return false;
                }

                lightData.CopyFrom(lightRaw);
            }

            // Block entity section
            ReadBlockEntities(reader, ms, out blockEntities, blockEntityRegistry);

            return true;
        }

        /// <summary>Reads the block entity section from the stream.</summary>
        private static void ReadBlockEntities(
            BinaryReader reader,
            MemoryStream ms,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry)
        {
            blockEntities = null;

            if (ms.Position < ms.Length)
            {
                int entityCount = reader.ReadInt32();

                if (entityCount > 0 && blockEntityRegistry is not null)
                {
                    blockEntities = new Dictionary<int, IBlockEntity>(entityCount);

                    for (int i = 0; i < entityCount; i++)
                    {
                        int flatIndex = reader.ReadInt32();
                        string typeId = reader.ReadString();
                        int payloadLen = reader.ReadInt32();
                        byte[] payload = reader.ReadBytes(payloadLen);

                        IBlockEntity entity = blockEntityRegistry.CreateEntity(typeId);

                        if (entity is not null)
                        {
                            using (MemoryStream entityMs = new(payload))
                            using (BinaryReader entityReader = new(entityMs))
                            {
                                entity.Deserialize(entityReader);
                            }

                            blockEntities[flatIndex] = entity;
                        }
                    }
                }
                else if (entityCount > 0)
                {
                    // Skip entity data if no registry provided
                    for (int i = 0; i < entityCount; i++)
                    {
                        reader.ReadInt32();  // flatIndex
                        reader.ReadString(); // typeId
                        int payloadLen = reader.ReadInt32();
                        reader.ReadBytes(payloadLen);
                    }
                }
            }
        }
    }
}
