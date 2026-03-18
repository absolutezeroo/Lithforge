using System;
using System.Collections.Generic;

using Lithforge.Network.Client;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

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
            // All rendering modes need chunk handling (SP/Host via DirectTransport, Client via UTP)
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            NetworkClient client = context.Get<NetworkClient>();

            SessionInitArgs args = SessionInitArgsHolder.Current;
            LoadingScreen loadingScreen = args?.LoadingScreen;

            // Capture player references for GameReady teleport
            PlayerTransformHolder player =
                context.TryGet(out PlayerTransformHolder p) ? p : null;

            _handler = new ClientChunkHandler(
                chunkManager, client,
                msg =>
                {
                    UnityEngine.Debug.Log(
                        $"[Lithforge] GameReady: spawn=({msg.SpawnX},{msg.SpawnY},{msg.SpawnZ})");

                    // Teleport player to server-assigned spawn position
                    if (player != null)
                    {
                        UnityEngine.Vector3 spawnPos = new(msg.SpawnX, msg.SpawnY, msg.SpawnZ);
                        player.Transform.position = spawnPos;

                        if (player.PhysicsBody != null)
                        {
                            player.PhysicsBody.Teleport(new float3(
                                msg.SpawnX, msg.SpawnY, msg.SpawnZ));
                            player.PhysicsBody.SpawnReady = true;
                        }
                    }

                    // Transition client to Playing state so input/block commands are sent
                    client.TransitionToPlaying();

                    // Dismiss loading screen (Client mode has no SpawnManager)
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
