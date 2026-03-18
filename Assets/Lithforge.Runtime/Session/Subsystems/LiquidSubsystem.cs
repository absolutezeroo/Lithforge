using System;
using System.Collections.Generic;

using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Liquid;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class LiquidSubsystem : IGameSubsystem
    {
        private LiquidPool _pool;
        private LiquidScheduler _scheduler;

        public string Name
        {
            get
            {
                return "Liquid";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

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

        public void PostInitialize(SessionContext context)
        {
            if (context.TryGet(out MeshScheduler meshScheduler))
            {
                _scheduler.SetMeshScheduler(meshScheduler);
            }

            ChunkManager chunkManager = context.Get<ChunkManager>();
            chunkManager.OnBlockChanged += _scheduler.OnBlockChanged;
        }

        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

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
