using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class AsyncChunkSaverSubsystem : IGameSubsystem
    {
        private AsyncChunkSaver _saver;

        public string Name
        {
            get
            {
                return "AsyncChunkSaver";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(StorageSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            _saver = new AsyncChunkSaver(worldStorage, context.App.Logger);
            context.Register(_saver);
        }

        public void PostInitialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            chunkManager.SetAsyncSaver(_saver);
        }

        public void Shutdown()
        {
            _saver?.Flush();
        }

        public void Dispose()
        {
            if (_saver != null)
            {
                _saver.Dispose();
                _saver = null;
            }
        }
    }
}
