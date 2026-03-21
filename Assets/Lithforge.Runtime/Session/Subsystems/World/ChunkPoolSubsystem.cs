using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the NativeArray chunk pool for recycling chunk allocations.</summary>
    public sealed class ChunkPoolSubsystem : IGameSubsystem
    {
        /// <summary>The owned chunk pool instance.</summary>
        private ChunkPool _pool;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ChunkPool";
            }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the chunk pool with configured size and registers it.</summary>
        public void Initialize(SessionContext context)
        {
            _pool = new ChunkPool(context.App.Settings.Chunk.PoolSize);
            context.Register(_pool);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the chunk pool and all its NativeArray allocations.</summary>
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
