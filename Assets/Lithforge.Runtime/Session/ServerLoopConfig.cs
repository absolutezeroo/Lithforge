using System;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Flat config bag for <see cref="ServerLoopPoco" />. Contains server-side references
    ///     that drive world generation, loading, and gameplay ticking for singleplayer/host.
    /// </summary>
    public sealed class ServerLoopConfig
    {
        /// <summary>Manages chunk lifecycle, loading queue, and unloading.</summary>
        public ChunkManager ChunkManager { get; set; }

        /// <summary>Persistence layer for reading and writing chunk data to disk.</summary>
        public WorldStorage WorldStorage { get; set; }

        /// <summary>Schedules and polls Burst generation jobs for new chunks.</summary>
        public GenerationScheduler GenerationScheduler { get; set; }

        /// <summary>Schedules cross-chunk light update jobs after block edits.</summary>
        public RelightScheduler RelightScheduler { get; set; }

        /// <summary>Schedules liquid flow simulation jobs.</summary>
        public LiquidScheduler LiquidScheduler { get; set; }

        /// <summary>Manages safe spawn point finding and teleportation.</summary>
        public SpawnManager SpawnManager { get; set; }

        /// <summary>Handles periodic auto-save of dirty chunks and metadata.</summary>
        public AutoSaveManager AutoSaveManager { get; set; }

        /// <summary>Round-robin tick scheduler for block entity behaviors.</summary>
        public BlockEntityTickScheduler BlockEntityTickScheduler { get; set; }

        /// <summary>Time budget in milliseconds allowed for chunk unloading per frame.</summary>
        public float UnloadBudgetMs { get; set; }

        /// <summary>Returns the chunk coordinate the server should center generation/loading around.</summary>
        public Func<int3> GetServerChunkCenter { get; set; }

        /// <summary>Returns the forward look direction for generation prioritization.</summary>
        public Func<float3> GetServerLookAhead { get; set; }
    }
}
