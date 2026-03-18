using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class ChunkManagerSubsystem : IGameSubsystem
    {
        private ChunkManager _manager;

        public string Name
        {
            get
            {
                return "ChunkManager";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkPoolSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

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

        public void PostInitialize(SessionContext context)
        {
            // Wire NativeStateRegistry for block entity flag checks
            _manager.SetNativeStateRegistry(context.Content.NativeStateRegistry);
        }

        public void Shutdown()
        {
        }

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
