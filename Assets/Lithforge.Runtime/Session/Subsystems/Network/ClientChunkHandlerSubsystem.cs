using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the client-side chunk handler for receiving and processing
    ///     chunk data from the server, plus the readiness tracker for spawn readiness.
    /// </summary>
    public sealed class ClientChunkHandlerSubsystem : IGameSubsystem
    {
        /// <summary>The owned client chunk handler instance.</summary>
        private ClientChunkHandler _handler;

        /// <summary>Tracks whether enough chunks are loaded around spawn for readiness.</summary>
        private ClientReadinessTracker _readinessTracker;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ClientChunkHandler";
            }
        }

        /// <summary>Depends on network client, chunk manager, and HUD for chunk handling.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(NetworkClientSubsystem),
            typeof(ChunkManagerSubsystem),
            typeof(HudSubsystem),
        };

        /// <summary>Created for all rendering modes (SP/Host via DirectTransport, Client via UTP).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // All rendering modes need chunk handling (SP/Host via DirectTransport, Client via UTP)
            return config.RequiresRendering;
        }

        /// <summary>Creates the readiness tracker, registers SpawnInit/GameReady handlers, and builds the chunk handler.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            NetworkClient client = context.Get<NetworkClient>();
            ChunkSettings chunkSettings = context.App.Settings.Chunk;

            // Capture player references for GameReady teleport
            PlayerTransformHolder player =
                context.TryGet(out PlayerTransformHolder p) ? p : null;

            // Capture loading screen so the GameReady callback can dismiss it
            SessionInitArgs args = SessionInitArgsHolder.Current;
            LoadingScreen loadingScreen = args?.LoadingScreen;

            // Create client-side readiness tracker. Uses poll-based design:
            // works identically for SP/Host (chunks arrive via LocalChunkStreamingStrategy)
            // and remote clients (chunks arrive via ChunkData messages) because both
            // ultimately populate the same ChunkManager.
            _readinessTracker = new ClientReadinessTracker(coord =>
            {
                ManagedChunk chunk = chunkManager.GetChunk(coord);

                return chunk is
                {
                    State: >= ChunkState.Generated,
                };
            })
            {
                // When all required chunks are available, send ClientReady to server
                OnReadinessAchieved = () =>
                {
                    ClientReadyMessage readyMsg = new()
                    {
                        ReadyRadius = (byte)chunkSettings.ClientReadyRadius,
                    };
                    client.Send(readyMsg, PipelineId.ReliableSequenced);
                },
            };

            // Register SpawnInit handler: server sends spawn position + ready radius
            // before streaming chunks, so the tracker can configure its required volume.
            client.Dispatcher.RegisterHandler(MessageType.SpawnInit, (connId, data, offset, length) =>
            {
                SpawnInitMessage spawnInit = SpawnInitMessage.Deserialize(data, offset, length);
                int3 spawnChunk = new(
                    (int)math.floor(spawnInit.SpawnX / ChunkConstants.Size),
                    (int)math.floor(spawnInit.SpawnY / ChunkConstants.Size),
                    (int)math.floor(spawnInit.SpawnZ / ChunkConstants.Size));

                // Narrow Y range to spawn chunk ± 1 so readiness doesn't wait for
                // deep underground chunks that the server may not have generated yet.
                int spawnY = spawnChunk.y;
                int readyYMin = math.max(chunkSettings.YLoadMin, spawnY - 1);
                int readyYMax = math.min(chunkSettings.YLoadMax, spawnY + 1);

                _readinessTracker.Configure(
                    spawnChunk,
                    spawnInit.ClientReadyRadius,
                    readyYMin,
                    readyYMax);

                // Create the client physics body now that we know the spawn position.
                // The body factory was registered by NetworkClientSubsystem.PostInitialize.
                if (context.TryGet(out ClientPlayerBodyFactory bodyFactory))
                {
                    float3 spawnPos = new(spawnInit.SpawnX, spawnInit.SpawnY, spawnInit.SpawnZ);
                    bodyFactory.CreateBody(spawnPos);
                }
            });

            _handler = new ClientChunkHandler(
                chunkManager, client,
                msg =>
                {
                    context.App.Logger.LogInfo(
                        $"[Lithforge] GameReady: spawn=({msg.SpawnX},{msg.SpawnY},{msg.SpawnZ})");

                    // Teleport player to server-assigned spawn position
                    if (player != null)
                    {
                        Vector3 spawnPos = new(msg.SpawnX, msg.SpawnY, msg.SpawnZ);
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

                    // Dismiss the loading screen
                    loadingScreen?.ForceComplete();
                });

            context.Register(_handler);
            context.Register(_readinessTracker);
        }

        /// <summary>Creates the block predictor and wires loading screen progress source.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Create block predictor for optimistic block placement
            ChunkManager chunkManager = context.Get<ChunkManager>();
            NetworkClient client = context.Get<NetworkClient>();

            ClientBlockPredictor predictor = new(chunkManager, client);
            context.Register(predictor);

            // Wire loading screen progress source (unified for SP/Host and remote)
            SessionInitArgs args = SessionInitArgsHolder.Current;
            LoadingScreen loadingScreen = args?.LoadingScreen;

            if (loadingScreen == null)
            {
                return;
            }

            Action onFadeComplete = null;

            if (context.TryGet(out HudVisibilityController hud))
            {
                onFadeComplete = () => { hud.ShowGameplay(); };
            }

            loadingScreen.SetProgressSource(() =>
            {
                SpawnReadinessSnapshot snapshot = _readinessTracker.GetSnapshot();

                return new SpawnProgress
                {
                    Phase = snapshot.IsComplete ? SpawnState.Done : SpawnState.Checking, ReadyChunks = snapshot.ReadyChunks, TotalChunks = snapshot.TotalChunks,
                };
            }, onFadeComplete);
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the client chunk handler.</summary>
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
