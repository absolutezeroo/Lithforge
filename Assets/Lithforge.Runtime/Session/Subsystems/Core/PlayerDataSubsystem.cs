using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the per-player data store and admin store
    ///     for multiplayer player persistence.
    /// </summary>
    public sealed class PlayerDataSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get { return "PlayerData"; }
        }

        /// <summary>Depends on storage subsystem for world directory access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(StorageSubsystem),
        };

        /// <summary>Only created for sessions with a local world to persist.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        /// <summary>Creates the player data store and admin store from the world directory.</summary>
        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            string worldDir = worldStorage.WorldDir;

            PlayerDataStore playerDataStore = new(worldDir);
            AdminStore adminStore = new(worldDir);
            adminStore.Load();

            context.Register(playerDataStore);
            context.Register(adminStore);
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
