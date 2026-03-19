using System;
using System.Collections.Generic;

using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Liquid;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the liquid simulation scheduler and pool for water flow.</summary>
    public sealed class LiquidSubsystem : IGameSubsystem
    {
        /// <summary>The owned liquid NativeArray pool.</summary>
        private LiquidPool _pool;

        /// <summary>The owned liquid simulation scheduler.</summary>
        private LiquidScheduler _scheduler;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Liquid";
            }
        }

        /// <summary>Depends on chunk manager for chunk data access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the liquid pool, resolves water state, and builds the scheduler.</summary>
        public void Initialize(SessionContext context)
        {
            _pool = new LiquidPool(64);

            StateId waterSourceId = StateIdHelper.FindStateId(context.Content, "lithforge:water");
            LiquidJobConfig waterConfig = LiquidJobConfig.Water(waterSourceId.Value);

            ChunkManager chunkManager = context.Get<ChunkManager>();

            _scheduler = new LiquidScheduler(
                chunkManager,
                _pool,
                context.Content.NativeStateRegistry,
                waterConfig);

            context.Register(_scheduler);
            context.Register(_pool);
        }

        /// <summary>Wires the liquid scheduler to block change events and the mesh scheduler.</summary>
        public void PostInitialize(SessionContext context)
        {
            if (context.TryGet(out MeshScheduler meshScheduler))
            {
                _scheduler.SetMeshScheduler(meshScheduler);
            }

            ChunkManager chunkManager = context.Get<ChunkManager>();
            chunkManager.OnBlockChanged += _scheduler.OnBlockChanged;
        }

        /// <summary>Completes all in-flight liquid simulation jobs.</summary>
        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

        /// <summary>Disposes the liquid pool and its NativeArray allocations.</summary>
        public void Dispose()
        {
            if (_pool != null)
            {
                _pool.Dispose();
                _pool = null;
            }
        }
    }
}
