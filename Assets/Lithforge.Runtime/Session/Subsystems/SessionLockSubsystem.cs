using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that acquires a filesystem lock to prevent concurrent world access.</summary>
    public sealed class SessionLockSubsystem : IGameSubsystem
    {
        /// <summary>The owned session lock file handle.</summary>
        private SessionLockHandle _handle;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "SessionLock";
            }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Only created for sessions with a local world directory.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        /// <summary>Attempts to acquire the session lock for the world directory.</summary>
        public void Initialize(SessionContext context)
        {
            string worldPath = context.Config switch
            {
                SessionConfig.Singleplayer sp => sp.WorldPath,
                SessionConfig.Host host => host.WorldPath,
                SessionConfig.DedicatedServer ds => ds.WorldPath,
                _ => null,
            };

            if (worldPath != null && !SessionLock.TryAcquire(worldPath, out _handle))
            {
                UnityEngine.Debug.LogError(
                    $"[Lithforge] Could not acquire session lock for {worldPath}. " +
                    "World may be open in another instance.");
            }

            if (_handle != null)
            {
                context.Register(_handle);
            }
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Releases the session lock file handle.</summary>
        public void Dispose()
        {
            if (_handle != null)
            {
                _handle.Dispose();
                _handle = null;
            }
        }
    }
}
