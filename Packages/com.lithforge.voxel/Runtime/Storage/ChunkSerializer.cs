using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using K4os.Compression.LZ4;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;

using Unity.Collections;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Serializes and deserializes chunk data using a binary format with palette compression,
    ///     LZ4 block compression, and CRC-32 integrity checking. Format version 4
    ///     (backward-compatible with v3 for deserialization). Thread-safe via ThreadStatic scratch buffers.
    /// </summary>
    public static class ChunkSerializer
    {
        /// <summary>Current binary format version.</summary>
        private const byte Version = 4;

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

        /// <summary>Per-thread scratch buffer for LZ4 compressed output.</summary>
        [ThreadStatic] private static byte[] s_compressBuffer;

        /// <summary>
        ///     Ensures the compress buffer is large enough for the given uncompressed size.
        /// </summary>
        private static void EnsureCompressBuffer(int uncompressedSize)
        {
            int maxOutput = LZ4Codec.MaximumOutputSize(uncompressedSize);

            if (s_compressBuffer == null || s_compressBuffer.Length < maxOutput)
            {
                s_compressBuffer = new byte[maxOutput];
            }
        }

        /// <summary>
        ///     Serializes chunk data from NativeArrays (main-thread path).
        ///     Palette-compresses voxel data, LZ4-compresses both voxel and light,
        ///     includes block entities, and appends a CRC-32 checksum. Format version 4.
        /// </summary>
        public static byte[] Serialize(
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities = null)
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

            // Compress voxel data (palette indices) with LZ4Codec
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

            EnsureCompressBuffer(voxelByteCount);
            int voxelCompLen = LZ4Codec.Encode(
                s_voxelBuffer, 0, voxelByteCount,
                s_compressBuffer, 0, s_compressBuffer.Length);
            writer.Write(voxelByteCount);
            writer.Write(voxelCompLen);
            writer.Write(s_compressBuffer, 0, voxelCompLen);

            // Compress light data with LZ4Codec
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

                EnsureCompressBuffer(lightByteCount);
                int lightCompLen = LZ4Codec.Encode(
                    s_lightBuffer, 0, lightByteCount,
                    s_compressBuffer, 0, s_compressBuffer.Length);
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
        ///     Serializes chunk data from raw byte[] snapshots (used by AsyncChunkSaver worker thread).
        ///     voxelSnapshot contains StateId values as little-endian ushorts (2 bytes each).
        /// </summary>
        public static byte[] Serialize(
            byte[] voxelSnapshot,
            int voxelCount,
            byte[] lightSnapshot,
            Dictionary<int, IBlockEntity> blockEntities = null)
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

            // Compress voxel data (palette indices) with LZ4Codec
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

            EnsureCompressBuffer(voxelByteCount);
            int voxelCompLen = LZ4Codec.Encode(
                s_voxelBuffer, 0, voxelByteCount,
                s_compressBuffer, 0, s_compressBuffer.Length);
            writer.Write(voxelByteCount);
            writer.Write(voxelCompLen);
            writer.Write(s_compressBuffer, 0, voxelCompLen);

            // Compress light data with LZ4Codec
            if (lightSnapshot is
                {
                    Length: > 0,
                })
            {
                int lightByteCount = lightSnapshot.Length;
                EnsureCompressBuffer(lightByteCount);
                int lightCompLen = LZ4Codec.Encode(
                    lightSnapshot, 0, lightByteCount,
                    s_compressBuffer, 0, s_compressBuffer.Length);
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
        ///     Supports format versions 3 and 4.
        ///     Returns false if the data is corrupted, truncated, or version-mismatched.
        /// </summary>
        public static bool Deserialize(byte[] data,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            out Dictionary<int, IBlockEntity> blockEntities,
            BlockEntityRegistry blockEntityRegistry = null)
        {
            blockEntities = null;

            if (data == null || data.Length < 9)
            {
                return false;
            }

            // Verify CRC32: last 4 bytes are the checksum
            if (data.Length < 13) // 4 magic + 1 version + 4 min content + 4 crc
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
                // Verify magic
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

                if (version != 4)
                {
                    return false;
                }

                // Version 4 path
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

                if (s_compressBuffer == null || s_compressBuffer.Length < compVoxelLen)
                {
                    s_compressBuffer = new byte[Math.Max(compVoxelLen, ChunkConstants.Volume * 2)];
                }

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
                    if (s_compressBuffer.Length < compLightLen)
                    {
                        s_compressBuffer = new byte[compLightLen];
                    }

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
