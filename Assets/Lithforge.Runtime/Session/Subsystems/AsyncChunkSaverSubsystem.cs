using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the async chunk saver for background persistence
    ///     of dirty chunks to disk without blocking the main thread.
    /// </summary>
    public sealed class AsyncChunkSaverSubsystem : IGameSubsystem
    {
        /// <summary>The owned async chunk saver instance.</summary>
        private AsyncChunkSaver _saver;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "AsyncChunkSaver";
            }
        }

        /// <summary>Depends on storage and chunk manager for persistence wiring.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(StorageSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Only created for sessions with a local world to persist.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        /// <summary>Creates the async saver from world storage and registers it.</summary>
        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            _saver = new AsyncChunkSaver(worldStorage, context.App.Logger);
            context.Register(_saver);
        }

        /// <summary>Wires the async saver into the chunk manager for automatic persistence.</summary>
        public void PostInitialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            chunkManager.SetAsyncSaver(_saver);
        }

        /// <summary>Flushes all pending async save operations before shutdown.</summary>
        public void Shutdown()
        {
            _saver?.Flush();
        }

        /// <summary>Disposes the async chunk saver and its background thread.</summary>
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
