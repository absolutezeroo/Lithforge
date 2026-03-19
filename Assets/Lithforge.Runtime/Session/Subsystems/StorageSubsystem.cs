using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates world storage and loads or creates world metadata.</summary>
    public sealed class StorageSubsystem : IGameSubsystem
    {
        /// <summary>The owned world storage instance.</summary>
        private WorldStorage _worldStorage;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Storage";
            }
        }

        /// <summary>Depends on session lock to prevent concurrent access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(SessionLockSubsystem),
        };

        /// <summary>Only created for sessions with a local world directory.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        /// <summary>Opens world storage, loads or creates metadata, and registers both.</summary>
        public void Initialize(SessionContext context)
        {
            string worldPath;
            string displayName;
            long seed;
            GameMode gameMode;

            switch (context.Config)
            {
                case SessionConfig.Singleplayer sp:
                    worldPath = sp.WorldPath;
                    displayName = sp.DisplayName;
                    seed = sp.Seed;
                    gameMode = sp.GameMode;
                    break;
                case SessionConfig.Host host:
                    worldPath = host.WorldPath;
                    displayName = host.DisplayName;
                    seed = host.Seed;
                    gameMode = host.GameMode;
                    break;
                case SessionConfig.DedicatedServer ds:
                    worldPath = ds.WorldPath;
                    displayName = "Dedicated Server";
                    seed = ds.Seed;
                    gameMode = GameMode.Survival;
                    break;
                default:
                    return;
            }

            _worldStorage = new WorldStorage(worldPath, context.App.Logger);

            WorldMetadata metadata = _worldStorage.LoadMetadata();

            if (metadata == null)
            {
                metadata = new WorldMetadata
                {
                    DisplayName = displayName ?? "New World", Seed = seed, GameMode = gameMode,
                };
                _worldStorage.SaveMetadataFull(metadata);
            }

            UnityEngine.Debug.Log(
                $"[Lithforge] World storage: {worldPath} (seed={metadata.Seed}, mode={metadata.GameMode})");

            context.Register(_worldStorage);
            context.Register(metadata);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes world storage and flushes any pending region writes.</summary>
        public void Dispose()
        {
            if (_worldStorage != null)
            {
                _worldStorage.Dispose();
                _worldStorage = null;
            }
        }
    }
}
