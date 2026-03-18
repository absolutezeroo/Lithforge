using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class SessionLockSubsystem : IGameSubsystem
    {
        private SessionLockHandle _handle;

        public string Name
        {
            get
            {
                return "SessionLock";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

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
