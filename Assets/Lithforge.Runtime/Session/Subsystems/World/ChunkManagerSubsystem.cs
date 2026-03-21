using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the chunk manager for chunk lifecycle, loading, and unloading.</summary>
    public sealed class ChunkManagerSubsystem : IGameSubsystem
    {
        /// <summary>The owned chunk manager instance.</summary>
        private ChunkManager _manager;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ChunkManager";
            }
        }

        /// <summary>Depends on chunk pool for NativeArray recycling.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkPoolSubsystem),
        };

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the chunk manager with configured render distance and Y-bounds.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkSettings cs = context.App.Settings.Chunk;
            ChunkPool pool = context.Get<ChunkPool>();

            _manager = new ChunkManager(
                pool,
                cs.RenderDistance,
                cs.YLoadMin,
                cs.YLoadMax,
                cs.YUnloadMin,
                cs.YUnloadMax);

            context.Register(_manager);
        }

        /// <summary>Wires the NativeStateRegistry for block entity flag checks.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Wire NativeStateRegistry for block entity flag checks
            _manager.SetNativeStateRegistry(context.Content.NativeStateRegistry);
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the chunk manager and all loaded chunks.</summary>
        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.Dispose();
                _manager = null;
            }
        }
    }
}
