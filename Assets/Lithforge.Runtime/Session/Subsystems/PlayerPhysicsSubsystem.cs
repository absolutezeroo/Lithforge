using System;
using System.Collections.Generic;

using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the player physics manager for collision detection.</summary>
    public sealed class PlayerPhysicsSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "PlayerPhysics";
            }
        }

        /// <summary>Depends on chunk manager for voxel collision queries.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the player physics manager from chunk manager and state registry.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();

            PlayerPhysicsManager manager = new(
                chunkManager, context.Content.NativeStateRegistry);

            context.Register(manager);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources.</summary>
        public void Dispose()
        {
        }
    }
}
