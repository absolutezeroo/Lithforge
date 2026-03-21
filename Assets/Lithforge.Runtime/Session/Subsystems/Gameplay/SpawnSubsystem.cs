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
    /// <summary>
    ///     Subsystem that creates the spawn manager for safe spawn point finding.
    ///     Currently disabled in always-server mode; spawn is driven by GameReady messages.
    /// </summary>
    public sealed class SpawnSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Spawn";
            }
        }

        /// <summary>Depends on player and chunk manager for spawn point calculation.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Currently disabled; always-server mode uses GameReady for spawn.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // In always-server mode, SP and Host use server-driven spawn via GameReady message.
            // SpawnManager is only needed for DedicatedServer (no rendering, no GameReady handler).
            return false;
        }

        /// <summary>Creates the spawn manager from player and chunk manager.</summary>
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources.</summary>
        public void Dispose()
        {
        }
    }
}
