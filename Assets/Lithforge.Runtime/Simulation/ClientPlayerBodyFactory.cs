using System;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Session;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Deferred factory that creates the client-side <see cref="PlayerPhysicsBody" />
    ///     when the server sends SpawnInit with the spawn position. Registered in
    ///     <see cref="SessionContext" /> by <c>NetworkClientSubsystem.PostInitialize</c>,
    ///     invoked by <c>ClientChunkHandlerSubsystem</c>'s SpawnInit handler.
    /// </summary>
    public sealed class ClientPlayerBodyFactory
    {
        /// <summary>Callback that performs the actual body creation and wiring.</summary>
        private readonly Action<float3> _createCallback;

        /// <summary>Whether the body has already been created (guards against double invocation).</summary>
        private bool _created;

        /// <summary>Creates a new factory with the given body creation callback.</summary>
        public ClientPlayerBodyFactory(Action<float3> createCallback)
        {
            _createCallback = createCallback;
        }

        /// <summary>
        ///     Creates the client physics body at the given spawn position.
        ///     Safe to call multiple times — only the first invocation takes effect.
        /// </summary>
        public void CreateBody(float3 spawnPosition)
        {
            if (_created)
            {
                return;
            }

            _created = true;
            _createCallback(spawnPosition);
        }
    }
}
