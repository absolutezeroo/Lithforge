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
    public static class ChunkSerializer
    {
        private const byte Version = 3;
        private static readonly byte[] s_magic =
        {
            (byte)'L',
            (byte)'F',
            (byte)'C',
            (byte)'H',
        };

        [ThreadStatic] private static byte[] s_voxelBuffer;
        [ThreadStatic] private static byte[] s_lightBuffer;
        [ThreadStatic] private static MemoryStream s_stream;

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

            // Compress voxel data (palette indices) with LZ4
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

            byte[] voxelCompressed = LZ4Pickler.Pickle(s_voxelBuffer);
            writer.Write(voxelCompressed.Length);
            writer.Write(voxelCompressed);

            // Compress light data with LZ4
            if (lightData.IsCreated && lightData.Length > 0)
            {
                if (s_lightBuffer == null || s_lightBuffer.Length < lightData.Length)
                {
                    s_lightBuffer = new byte[ChunkConstants.Volume];
                }

                lightData.CopyTo(s_lightBuffer);

                byte[] lightCompressed = LZ4Pickler.Pickle(s_lightBuffer);
                writer.Write(lightCompressed.Length);
                writer.Write(lightCompressed);
            }
            else
            {
                writer.Write(0);
            }

            // Block entity section
            WriteBlockEntities(writer, blockEntities);

            // Append CRC32 checksum of all preceding bytes
            writer.Flush();
            byte[] payload = s_stream.ToArray();
            uint crc = Crc32.Compute(payload, 0, payload.Length);

            writer.Write(crc);

            byte[] result = s_stream.ToArray();
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

            // Compress voxel data (palette indices) with LZ4
            if (s_voxelBuffer == null || s_voxelBuffer.Length < voxelCount * 2)
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

            byte[] voxelCompressed = LZ4Pickler.Pickle(s_voxelBuffer);
            writer.Write(voxelCompressed.Length);
            writer.Write(voxelCompressed);

            // Compress light data with LZ4
            if (lightSnapshot != null && lightSnapshot.Length > 0)
            {
                byte[] lightCompressed = LZ4Pickler.Pickle(lightSnapshot);
                writer.Write(lightCompressed.Length);
                writer.Write(lightCompressed);
            }
            else
            {
                writer.Write(0);
            }

            // Block entity section
            WriteBlockEntities(writer, blockEntities);

            // Append CRC32 checksum of all preceding bytes
            writer.Flush();
            byte[] payload = s_stream.ToArray();
            uint crc = Crc32.Compute(payload, 0, payload.Length);

            writer.Write(crc);

            byte[] result = s_stream.ToArray();
            writer.Dispose();

            return result;
        }

        private static void WriteBlockEntities(BinaryWriter writer, Dictionary<int, IBlockEntity> blockEntities)
        {
            if (blockEntities != null && blockEntities.Count > 0)
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

                if (version != 3)
                {
                    return false;
                }

                // Read palette
                ushort paletteCount = reader.ReadUInt16();
                ushort[] palette = new ushort[paletteCount];

                for (int i = 0; i < paletteCount; i++)
                {
                    palette[i] = reader.ReadUInt16();
                }

                // Decompress voxel data with LZ4
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

                // Decompress light data with LZ4
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
                if (ms.Position < ms.Length)
                {
                    int entityCount = reader.ReadInt32();

                    if (entityCount > 0 && blockEntityRegistry != null)
                    {
                        blockEntities = new Dictionary<int, IBlockEntity>(entityCount);

                        for (int i = 0; i < entityCount; i++)
                        {
                            int flatIndex = reader.ReadInt32();
                            string typeId = reader.ReadString();
                            int payloadLen = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(payloadLen);

                            IBlockEntity entity = blockEntityRegistry.CreateEntity(typeId);

                            if (entity != null)
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

                return true;
            }
        }
    }
}
