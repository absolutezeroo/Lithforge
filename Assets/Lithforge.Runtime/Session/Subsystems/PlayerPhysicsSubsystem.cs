using System;
using System.Collections.Generic;

using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class PlayerPhysicsSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "PlayerPhysics";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();

            PlayerPhysicsManager manager = new(
                chunkManager, context.Content.NativeStateRegistry);

            context.Register(manager);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
