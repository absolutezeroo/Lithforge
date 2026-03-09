using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Jobs
{
    /// <summary>
    /// Burst PoC job: fills a NativeArray&lt;StateId&gt; with stone below a given height
    /// and air above. Proves the Burst pipeline works with Lithforge types.
    ///
    /// This job iterates all voxels in a 32x32x32 chunk and sets each one
    /// to stone (if y &lt; surfaceHeight) or air (otherwise).
    /// </summary>
    [BurstCompile]
    public struct FillColumnJob : IJob
    {
        public NativeArray<StateId> ChunkStates;

        [ReadOnly] public StateId StoneState;
        [ReadOnly] public StateId AirState;
        [ReadOnly] public int SurfaceHeight;

        public void Execute()
        {
            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                StateId state = y < SurfaceHeight ? StoneState : AirState;

                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int index = ChunkData.GetIndex(x, y, z);

                        ChunkStates[index] = state;
                    }
                }
            }
        }
    }
}
