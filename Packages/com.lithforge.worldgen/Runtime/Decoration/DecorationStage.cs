using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    public sealed class DecorationStage
    {
        private readonly NativeArray<NativeBiomeData> _biomeData;
        private readonly TreeBlock[] _oakTreeTemplate;
        private readonly StateId _airId;
        private readonly PendingDecorationStore _pendingStore;

        public PendingDecorationStore PendingStore
        {
            get { return _pendingStore; }
        }

        public DecorationStage(
            NativeArray<NativeBiomeData> biomeData,
            StateId oakLogId,
            StateId oakLeavesId,
            StateId airId)
        {
            _biomeData = biomeData;
            _oakTreeTemplate = TreeTemplate.OakTree(oakLogId, oakLeavesId);
            _airId = airId;
            _pendingStore = new PendingDecorationStore();
        }

        public void Decorate(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            NativeArray<int> heightMap,
            NativeArray<byte> biomeMap,
            long seed)
        {
            // Apply any pending decorations from neighboring chunks
            _pendingStore.ApplyPending(chunkCoord, chunkData);

            int chunkWorldX = chunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = chunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = chunkCoord.z * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    byte biomeId = biomeMap[columnIndex];
                    int surfaceY = heightMap[columnIndex];

                    NativeBiomeData biome = GetBiome(biomeId);

                    if (biome.TreeDensity <= 0.0f)
                    {
                        continue;
                    }

                    int worldX = chunkWorldX + x;
                    int worldZ = chunkWorldZ + z;

                    // Deterministic hash to decide if a tree spawns at this XZ
                    uint hash = Hash(worldX, worldZ, seed);
                    float roll = (hash & 0xFFFF) / 65535.0f;

                    if (roll >= biome.TreeDensity)
                    {
                        continue;
                    }

                    // Tree base is at surface Y (one block above highest non-air)
                    int treeBaseY = surfaceY;

                    // Only place tree if base is in this chunk's Y range
                    if (treeBaseY < chunkWorldY || treeBaseY >= chunkWorldY + ChunkConstants.Size)
                    {
                        continue;
                    }

                    int localBaseY = treeBaseY - chunkWorldY;

                    // Check that the base block is air (tree grows on top of surface)
                    int baseIndex = ChunkData.GetIndex(x, localBaseY, z);

                    if (!chunkData[baseIndex].Equals(_airId))
                    {
                        continue;
                    }

                    PlaceTree(chunkCoord, chunkData, x, localBaseY, z);
                }
            }
        }

        private void PlaceTree(
            int3 chunkCoord,
            NativeArray<StateId> chunkData,
            int baseX,
            int baseY,
            int baseZ)
        {
            for (int i = 0; i < _oakTreeTemplate.Length; i++)
            {
                TreeBlock block = _oakTreeTemplate[i];
                int localX = baseX + block.Offset.x;
                int localY = baseY + block.Offset.y;
                int localZ = baseZ + block.Offset.z;

                if (localX >= 0 && localX < ChunkConstants.Size &&
                    localY >= 0 && localY < ChunkConstants.Size &&
                    localZ >= 0 && localZ < ChunkConstants.Size)
                {
                    int index = ChunkData.GetIndex(localX, localY, localZ);
                    StateId current = chunkData[index];

                    // Only place tree blocks into air
                    if (current.Equals(_airId))
                    {
                        chunkData[index] = block.State;
                    }
                }
                else
                {
                    // Cross-chunk: compute target chunk coord and local position
                    int worldX = chunkCoord.x * ChunkConstants.Size + localX;
                    int worldY = chunkCoord.y * ChunkConstants.Size + localY;
                    int worldZ = chunkCoord.z * ChunkConstants.Size + localZ;

                    int3 targetChunk = new int3(
                        (int)math.floor((float)worldX / ChunkConstants.Size),
                        (int)math.floor((float)worldY / ChunkConstants.Size),
                        (int)math.floor((float)worldZ / ChunkConstants.Size));

                    int3 targetLocal = new int3(
                        ((worldX % ChunkConstants.Size) + ChunkConstants.Size) % ChunkConstants.Size,
                        ((worldY % ChunkConstants.Size) + ChunkConstants.Size) % ChunkConstants.Size,
                        ((worldZ % ChunkConstants.Size) + ChunkConstants.Size) % ChunkConstants.Size);

                    _pendingStore.Add(targetChunk, new PendingBlock
                    {
                        LocalPosition = targetLocal,
                        State = block.State,
                    });
                }
            }
        }

        private NativeBiomeData GetBiome(byte biomeId)
        {
            for (int i = 0; i < _biomeData.Length; i++)
            {
                if (_biomeData[i].BiomeId == biomeId)
                {
                    return _biomeData[i];
                }
            }

            return _biomeData[0];
        }

        private static uint Hash(int x, int z, long seed)
        {
            uint h = (uint)(seed & 0xFFFFFFFF);
            h ^= (uint)x * 374761393u;
            h ^= (uint)z * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            return h;
        }
    }
}
