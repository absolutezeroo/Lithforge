using System;
using System.Collections.Generic;

using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the unified chunk persistence service for background
    ///     serialization, write coalescing, and pristine chunk caching.
    /// </summary>
    public sealed class ChunkPersistenceSubsystem : IGameSubsystem
    {
        /// <summary>The owned persistence service instance.</summary>
        private ChunkPersistenceService _service;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ChunkPersistence";
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

        /// <summary>Creates the persistence service from world storage and registers it.</summary>
        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            _service = new ChunkPersistenceService(worldStorage, context.App.Logger);
            context.Register(_service);
        }

        /// <summary>Wires the persistence service into chunk manager, generation scheduler, and auto-save.</summary>
        public void PostInitialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            chunkManager.SetPersistenceService(_service);

            if (context.TryGet(out GenerationScheduler genScheduler))
            {
                genScheduler.SetPersistenceService(_service);
            }

            if (context.TryGet(out AutoSaveManager autoSave))
            {
                autoSave.SetPersistenceService(_service);
            }
        }

        /// <summary>Flushes all pending writes and cached pristine chunks before shutdown.</summary>
        public void Shutdown()
        {
            _service?.Flush();
        }

        /// <summary>Disposes the persistence service and its I/O thread.</summary>
        public void Dispose()
        {
            if (_service is not null)
            {
                _service.Dispose();
                _service = null;
            }
        }
    }
}
