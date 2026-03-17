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
        public int3 ChunkCoord;
        public JobHandle Handle;
        public NativeList<LiquidChunkEdit> OutputEdits;
        public NativeList<int> OutputActiveSet;
        public NativeArray<int> InputActiveSet;

        public NativeArray<byte> GhostPosX;
        public NativeArray<byte> GhostNegX;
        public NativeArray<byte> GhostPosY;
        public NativeArray<byte> GhostNegY;
        public NativeArray<byte> GhostPosZ;
        public NativeArray<byte> GhostNegZ;

        public NativeArray<byte> GhostBlockSolidPosX;
        public NativeArray<byte> GhostBlockSolidNegX;
        public NativeArray<byte> GhostBlockSolidPosZ;
        public NativeArray<byte> GhostBlockSolidNegZ;

        public int FrameAge;
        public byte Parity;

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
