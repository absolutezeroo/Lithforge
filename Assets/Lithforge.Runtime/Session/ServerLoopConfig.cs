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
        public ChunkManager ChunkManager { get; set; }

        public WorldStorage WorldStorage { get; set; }

        public GenerationScheduler GenerationScheduler { get; set; }

        public RelightScheduler RelightScheduler { get; set; }

        public LiquidScheduler LiquidScheduler { get; set; }

        public SpawnManager SpawnManager { get; set; }

        public AutoSaveManager AutoSaveManager { get; set; }

        public BlockEntityTickScheduler BlockEntityTickScheduler { get; set; }

        public float UnloadBudgetMs { get; set; }

        /// <summary>Returns the chunk coordinate the server should center generation/loading around.</summary>
        public Func<int3> GetServerChunkCenter { get; set; }

        /// <summary>Returns the forward look direction for generation prioritization.</summary>
        public Func<float3> GetServerLookAhead { get; set; }
    }
}
