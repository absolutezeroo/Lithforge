using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the auto-save manager for periodic world persistence.</summary>
    public sealed class AutoSaveSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "AutoSave";
            }
        }

        /// <summary>Depends on storage, player, and player data subsystems for save data access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(StorageSubsystem),
            typeof(PlayerSubsystem),
            typeof(PlayerDataSubsystem),
        };

        /// <summary>Only created for sessions with a local world to persist.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        /// <summary>Creates the auto-save manager from world storage and player state.</summary>
        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            WorldMetadata metadata = context.Get<WorldMetadata>();
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();

            AutoSaveManager autoSave = new(
                worldStorage,
                metadata,
                player.Transform,
                player.MainCamera,
                () => 0f, // TimeOfDay wired in PostInitialize
                player.Inventory);

            if (context.TryGet(out AsyncChunkSaver saver))
            {
                autoSave.SetAsyncSaver(saver);
            }

            if (context.TryGet(out PlayerDataStore playerDataStore))
            {
                autoSave.SetPlayerDataStore(playerDataStore);
            }

            context.Register(autoSave);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>Metadata save handled by SessionOrchestrator during quit.</summary>
        public void Shutdown()
        {
            // AutoSaveManager.SaveMetadataOnly called by SessionOrchestrator
        }

        /// <summary>No owned disposable resources.</summary>
        public void Dispose()
        {
        }
    }
}
