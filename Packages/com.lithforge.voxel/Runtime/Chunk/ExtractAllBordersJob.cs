using Lithforge.Voxel.Block;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Burst-compiled job that extracts all 6 border slices from neighboring chunks
    /// in a single Execute() call. Replaces 6 separate ExtractSingleBorderJob schedules
    /// to reduce Job System scheduling overhead (56 Schedule() calls → 8 per frame).
    ///
    /// Each neighbor input has a corresponding HasXxx flag. When false, the output
    /// is left as-is (zero-initialized by GreedyMeshData constructor = air).
    /// A dummy NativeArray is passed for missing neighbors to satisfy the safety system.
    ///
    /// Face direction mapping (inverted relative to neighbor offset):
    ///   +X neighbor → extract its -X face (x=0)  into OutputPosX
    ///   -X neighbor → extract its +X face (x=31) into OutputNegX
    ///   +Y neighbor → extract its -Y face (y=0)  into OutputPosY
    ///   -Y neighbor → extract its +Y face (y=31) into OutputNegY
    ///   +Z neighbor → extract its -Z face (z=0)  into OutputPosZ
    ///   -Z neighbor → extract its +Z face (z=31) into OutputNegZ
    ///
    /// Output layout: output[v * 32 + u].
    /// </summary>
    [BurstCompile]
    public struct ExtractAllBordersJob : IJob
    {
        /// <summary>Voxel data from the +X neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborPosXData;

        /// <summary>Voxel data from the -X neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborNegXData;

        /// <summary>Voxel data from the +Y neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborPosYData;

        /// <summary>Voxel data from the -Y neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborNegYData;

        /// <summary>Voxel data from the +Z neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborPosZData;

        /// <summary>Voxel data from the -Z neighbor chunk (or dummy if missing).</summary>
        [ReadOnly] public NativeArray<StateId> NeighborNegZData;

        /// <summary>Whether the +X neighbor exists (false = skip extraction, output stays zero).</summary>
        public bool HasPosX;

        /// <summary>Whether the -X neighbor exists.</summary>
        public bool HasNegX;

        /// <summary>Whether the +Y neighbor exists.</summary>
        public bool HasPosY;

        /// <summary>Whether the -Y neighbor exists.</summary>
        public bool HasNegY;

        /// <summary>Whether the +Z neighbor exists.</summary>
        public bool HasPosZ;

        /// <summary>Whether the -Z neighbor exists.</summary>
        public bool HasNegZ;

        /// <summary>Output: 1024-element border slice from the +X neighbor's -X face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputPosX;

        /// <summary>Output: 1024-element border slice from the -X neighbor's +X face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputNegX;

        /// <summary>Output: 1024-element border slice from the +Y neighbor's -Y face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputPosY;

        /// <summary>Output: 1024-element border slice from the -Y neighbor's +Y face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputNegY;

        /// <summary>Output: 1024-element border slice from the +Z neighbor's -Z face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputPosZ;

        /// <summary>Output: 1024-element border slice from the -Z neighbor's +Z face.</summary>
        [WriteOnly] public NativeArray<StateId> OutputNegZ;

        /// <summary>Extracts all 6 border slices from present neighbors in a single pass.</summary>
        public void Execute()
        {
            int size = ChunkConstants.Size;
            int lastIdx = size - 1;

            // +X neighbor: extract its -X face (FaceDirection=1: x=0, u=z, v=y)
            if (HasPosX)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(0, v, u);
                        OutputPosX[v * size + u] = NeighborPosXData[srcIndex];
                    }
                }
            }

            // -X neighbor: extract its +X face (FaceDirection=0: x=31, u=z, v=y)
            if (HasNegX)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(lastIdx, v, u);
                        OutputNegX[v * size + u] = NeighborNegXData[srcIndex];
                    }
                }
            }

            // +Y neighbor: extract its -Y face (FaceDirection=3: y=0, u=x, v=z)
            if (HasPosY)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(u, 0, v);
                        OutputPosY[v * size + u] = NeighborPosYData[srcIndex];
                    }
                }
            }

            // -Y neighbor: extract its +Y face (FaceDirection=2: y=31, u=x, v=z)
            if (HasNegY)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(u, lastIdx, v);
                        OutputNegY[v * size + u] = NeighborNegYData[srcIndex];
                    }
                }
            }

            // +Z neighbor: extract its -Z face (FaceDirection=5: z=0, u=x, v=y)
            if (HasPosZ)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(u, v, 0);
                        OutputPosZ[v * size + u] = NeighborPosZData[srcIndex];
                    }
                }
            }

            // -Z neighbor: extract its +Z face (FaceDirection=4: z=31, u=x, v=y)
            if (HasNegZ)
            {
                for (int v = 0; v < size; v++)
                {
                    for (int u = 0; u < size; u++)
                    {
                        int srcIndex = ChunkData.GetIndex(u, v, lastIdx);
                        OutputNegZ[v * size + u] = NeighborNegZData[srcIndex];
                    }
                }
            }
        }
    }
}
