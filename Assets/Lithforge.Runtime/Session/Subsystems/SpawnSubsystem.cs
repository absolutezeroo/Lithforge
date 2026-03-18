using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class SpawnSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "Spawn";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            // In always-server mode, SP and Host use server-driven spawn via GameReady message.
            // SpawnManager is only needed for DedicatedServer (no rendering, no GameReady handler).
            return false;
        }

        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            ChunkManager chunkManager = context.Get<ChunkManager>();
            ChunkSettings cs = context.App.Settings.Chunk;
            ILogger logger = context.App.Logger;

            int lod1Dist = SchedulingConfig.LOD1Distance(cs.RenderDistance);
            int spawnRadius = cs.SpawnLoadRadius;

            if (spawnRadius > lod1Dist)
            {
                logger.LogWarning(
                    $"Spawn radius ({spawnRadius}) exceeds LOD1 distance ({lod1Dist}). " +
                    $"Clamping to {lod1Dist} to ensure full-detail meshes in spawn area.");
                spawnRadius = lod1Dist;
            }

            SpawnManager spawnManager = new(
                chunkManager,
                context.Content.NativeStateRegistry,
                player.Transform,
                spawnRadius,
                cs.YLoadMin,
                cs.YLoadMax,
                cs.SpawnFallbackY);

            if (player.HasRestoredState)
            {
                spawnManager.SetSavedPosition();
            }

            context.Register(spawnManager);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
