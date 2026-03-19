using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Tracks one in-flight <see cref="LiquidSimJob"/>: its handle, output containers,
    /// and which chunk it belongs to. Owned by LiquidScheduler._inFlightJobs.
    ///
    /// All NativeContainers here are TempJob-allocated.
    /// Dispose order on completion:
    ///   1. OutputEdits.Dispose()
    ///   2. OutputActiveSet.Dispose()
    ///   3. InputActiveSet.Dispose()
    ///   4. GhostSlabs (10 arrays: 6 liquid + 4 block-solid) — Dispose()
    /// </summary>
    public struct PendingLiquidJob
    {
        /// <summary>Chunk coordinate this job is processing.</summary>
        public int3 ChunkCoord;

        /// <summary>Job system handle for completion polling.</summary>
        public JobHandle Handle;

        /// <summary>Output voxel edits produced by the liquid simulation.</summary>
        public NativeList<LiquidChunkEdit> OutputEdits;

        /// <summary>Output set of flat indices that remain active for next tick.</summary>
        public NativeList<int> OutputActiveSet;

        /// <summary>Input set of flat indices to process this tick.</summary>
        public NativeArray<int> InputActiveSet;

        /// <summary>Ghost slab: liquid data from the +X neighbor face.</summary>
        public NativeArray<byte> GhostPosX;

        /// <summary>Ghost slab: liquid data from the -X neighbor face.</summary>
        public NativeArray<byte> GhostNegX;

        /// <summary>Ghost slab: liquid data from the +Y neighbor face.</summary>
        public NativeArray<byte> GhostPosY;

        /// <summary>Ghost slab: liquid data from the -Y neighbor face.</summary>
        public NativeArray<byte> GhostNegY;

        /// <summary>Ghost slab: liquid data from the +Z neighbor face.</summary>
        public NativeArray<byte> GhostPosZ;

        /// <summary>Ghost slab: liquid data from the -Z neighbor face.</summary>
        public NativeArray<byte> GhostNegZ;

        /// <summary>Ghost slab: block solidity from the +X neighbor face.</summary>
        public NativeArray<byte> GhostBlockSolidPosX;

        /// <summary>Ghost slab: block solidity from the -X neighbor face.</summary>
        public NativeArray<byte> GhostBlockSolidNegX;

        /// <summary>Ghost slab: block solidity from the +Z neighbor face.</summary>
        public NativeArray<byte> GhostBlockSolidPosZ;

        /// <summary>Ghost slab: block solidity from the -Z neighbor face.</summary>
        public NativeArray<byte> GhostBlockSolidNegZ;

        /// <summary>Number of frames since this job was scheduled (for timeout detection).</summary>
        public int FrameAge;

        /// <summary>Alternating parity bit for double-buffered active set swapping.</summary>
        public byte Parity;

        /// <summary>Disposes all TempJob NativeContainers owned by this pending job.</summary>
        public void DisposeContainers()
        {
            if (OutputEdits.IsCreated)
            {
                OutputEdits.Dispose();
            }

            if (OutputActiveSet.IsCreated)
            {
                OutputActiveSet.Dispose();
            }

            if (InputActiveSet.IsCreated)
            {
                InputActiveSet.Dispose();
            }

            if (GhostPosX.IsCreated)
            {
                GhostPosX.Dispose();
            }

            if (GhostNegX.IsCreated)
            {
                GhostNegX.Dispose();
            }

            if (GhostPosY.IsCreated)
            {
                GhostPosY.Dispose();
            }

            if (GhostNegY.IsCreated)
            {
                GhostNegY.Dispose();
            }

            if (GhostPosZ.IsCreated)
            {
                GhostPosZ.Dispose();
            }

            if (GhostNegZ.IsCreated)
            {
                GhostNegZ.Dispose();
            }

            if (GhostBlockSolidPosX.IsCreated)
            {
                GhostBlockSolidPosX.Dispose();
            }

            if (GhostBlockSolidNegX.IsCreated)
            {
                GhostBlockSolidNegX.Dispose();
            }

            if (GhostBlockSolidPosZ.IsCreated)
            {
                GhostBlockSolidPosZ.Dispose();
            }

            if (GhostBlockSolidNegZ.IsCreated)
            {
                GhostBlockSolidNegZ.Dispose();
            }
        }
    }
}
