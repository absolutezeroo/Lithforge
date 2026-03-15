using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Physics
{
    /// <summary>
    /// Burst-compiled parallel collision resolution for multiple entities.
    /// Each entity gets its own pre-filled <see cref="SolidBlockQuery"/> (broad-phase region).
    ///
    /// Usage:
    ///   1. Fill EntityStates[i] with position, velocity, halfWidth, height
    ///   2. Fill Queries[i] with SolidBlockQuery built for that entity's broad-phase
    ///   3. Schedule the job
    ///   4. Read back EntityStates[i].Position, .Velocity, .Result
    ///   5. Dispose all SolidBlockQuery.SolidMap instances (caller responsibility)
    ///
    /// Owner: whoever schedules the job. Lifetime: single frame (TempJob).
    /// </summary>
    [BurstCompile]
    public struct VoxelColliderJob : IJobParallelFor
    {
        public NativeArray<EntityPhysicsState> EntityStates;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<SolidBlockQuery> Queries;

        public void Execute(int index)
        {
            EntityPhysicsState state = EntityStates[index];
            SolidBlockQuery query = Queries[index];

            state.Result = VoxelCollider.Resolve(
                ref state.Position,
                ref state.Velocity,
                state.HalfWidth,
                state.Height,
                query);

            EntityStates[index] = state;
        }
    }
}
