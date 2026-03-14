using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Unity.Collections;

namespace Lithforge.Voxel.Storage
{
    public static class ChunkSerializer
    {
        private static readonly byte[] _magic = { (byte)'L', (byte)'F', (byte)'C', (byte)'H' };
        private const byte _version = 2;

        public static byte[] Serialize(
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities = null)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Header
                writer.Write(_magic);
                writer.Write(_version);

                // Build palette
                Dictionary<ushort, ushort> paletteMap = new Dictionary<ushort, ushort>();
                List<ushort> paletteList = new List<ushort>();

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

                // Compress voxel data (palette indices)
                byte[] voxelBytes;

                using (MemoryStream voxelMs = new MemoryStream())
                {
                    using (DeflateStream deflate = new DeflateStream(voxelMs, CompressionLevel.Fastest, true))
                    using (BinaryWriter voxelWriter = new BinaryWriter(deflate))
                    {
                        for (int i = 0; i < chunkData.Length; i++)
                        {
                            voxelWriter.Write(paletteMap[chunkData[i].Value]);
                        }
                    }

                    voxelBytes = voxelMs.ToArray();
                }

                writer.Write(voxelBytes.Length);
                writer.Write(voxelBytes);

                // Compress light data
                if (lightData.IsCreated && lightData.Length > 0)
                {
                    byte[] lightBytes;

                    using (MemoryStream lightMs = new MemoryStream())
                    {
                        using (DeflateStream deflate = new DeflateStream(lightMs, CompressionLevel.Fastest, true))
                        {
                            byte[] raw = new byte[lightData.Length];
                            lightData.CopyTo(raw);
                            deflate.Write(raw, 0, raw.Length);
                        }

                        lightBytes = lightMs.ToArray();
                    }

                    writer.Write(lightBytes.Length);
                    writer.Write(lightBytes);
                }
                else
                {
                    writer.Write(0);
                }

                // Block entity section (version 2+)
                if (blockEntities != null && blockEntities.Count > 0)
                {
                    writer.Write(blockEntities.Count);

                    foreach (KeyValuePair<int, IBlockEntity> kvp in blockEntities)
                    {
                        writer.Write(kvp.Key); // flat index
                        writer.Write(kvp.Value.TypeId); // type ID string

                        // Length-prefixed entity payload
                        using (MemoryStream entityMs = new MemoryStream())
                        using (BinaryWriter entityWriter = new BinaryWriter(entityMs))
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

                return ms.ToArray();
            }
        }

        public static bool Deserialize(
            byte[] data,
            NativeArray<StateId> chunkData,
            NativeArray<byte> lightData,
            BlockEntityRegistry blockEntityRegistry = null,
            out Dictionary<int, IBlockEntity> blockEntities)
        {
            blockEntities = null;

            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // Verify magic
                byte[] magic = reader.ReadBytes(4);

                if (magic.Length != 4 ||
                    magic[0] != _magic[0] || magic[1] != _magic[1] ||
                    magic[2] != _magic[2] || magic[3] != _magic[3])
                {
                    return false;
                }

                byte version = reader.ReadByte();

                if (version < 1 || version > 2)
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

                // Decompress voxel data
                int compressedVoxelLen = reader.ReadInt32();
                byte[] compressedVoxels = reader.ReadBytes(compressedVoxelLen);

                using (MemoryStream voxelMs = new MemoryStream(compressedVoxels))
                using (DeflateStream inflate = new DeflateStream(voxelMs, CompressionMode.Decompress))
                using (BinaryReader voxelReader = new BinaryReader(inflate))
                {
                    for (int i = 0; i < chunkData.Length; i++)
                    {
                        ushort paletteIdx = voxelReader.ReadUInt16();
                        chunkData[i] = new StateId(palette[paletteIdx]);
                    }
                }

                // Decompress light data
                int compressedLightLen = reader.ReadInt32();

                if (compressedLightLen > 0 && lightData.IsCreated)
                {
                    byte[] compressedLight = reader.ReadBytes(compressedLightLen);

                    using (MemoryStream lightMs = new MemoryStream(compressedLight))
                    using (DeflateStream inflate = new DeflateStream(lightMs, CompressionMode.Decompress))
                    {
                        byte[] raw = new byte[lightData.Length];
                        int totalRead = 0;

                        while (totalRead < raw.Length)
                        {
                            int bytesRead = inflate.Read(raw, totalRead, raw.Length - totalRead);

                            if (bytesRead == 0)
                            {
                                break;
                            }

                            totalRead += bytesRead;
                        }

                        lightData.CopyFrom(raw);
                    }
                }

                // Block entity section (version 2 only)
                if (version >= 2 && ms.Position < ms.Length)
                {
                    int entityCount = reader.ReadInt32();

                    if (entityCount > 0 && blockEntityRegistry != null)
                    {
                        blockEntities = new Dictionary<int, IBlockEntity>(entityCount);

                        for (int i = 0; i < entityCount; i++)
                        {
                            int flatIndex = reader.ReadInt32();
                            string typeId = reader.ReadString();
                            int payloadLength = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(payloadLength);

                            IBlockEntity entity = blockEntityRegistry.CreateEntity(typeId);

                            if (entity != null)
                            {
                                using (MemoryStream entityMs = new MemoryStream(payload))
                                using (BinaryReader entityReader = new BinaryReader(entityMs))
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
                            reader.ReadInt32(); // flatIndex
                            reader.ReadString(); // typeId
                            int payloadLength = reader.ReadInt32();
                            reader.ReadBytes(payloadLength);
                        }
                    }
                }

                return true;
            }
        }
    }
}
