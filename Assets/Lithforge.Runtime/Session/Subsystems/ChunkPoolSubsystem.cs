using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class ChunkPoolSubsystem : IGameSubsystem
    {
        private ChunkPool _pool;

        public string Name
        {
            get
            {
                return "ChunkPool";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        public void Initialize(SessionContext context)
        {
            _pool = new ChunkPool(context.App.Settings.Chunk.PoolSize);
            context.Register(_pool);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
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
