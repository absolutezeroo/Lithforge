using System;
using System.Collections.Generic;

using Lithforge.Network.Client;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class ClientChunkHandlerSubsystem : IGameSubsystem
    {
        private ClientChunkHandler _handler;

        public string Name
        {
            get
            {
                return "ClientChunkHandler";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(NetworkClientSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config is SessionConfig.Client;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            NetworkClient client = context.Get<NetworkClient>();

            SessionInitArgs args = SessionInitArgsHolder.Current;
            LoadingScreen loadingScreen = args?.LoadingScreen;

            _handler = new ClientChunkHandler(
                chunkManager, client,
                msg =>
                {
                    UnityEngine.Debug.Log(
                        $"[Lithforge] GameReady: spawn=({msg.SpawnX},{msg.SpawnY},{msg.SpawnZ})");

                    // Dismiss loading screen (Client mode has no SpawnManager to do this)
                    loadingScreen?.ForceComplete();
                });

            context.Register(_handler);
        }

        public void PostInitialize(SessionContext context)
        {
            // Create block predictor for optimistic block placement
            ChunkManager chunkManager = context.Get<ChunkManager>();
            NetworkClient client = context.Get<NetworkClient>();

            ClientBlockPredictor predictor = new(chunkManager, client);
            context.Register(predictor);
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            if (_handler != null)
            {
                _handler.Dispose();
                _handler = null;
            }
        }
    }
}
