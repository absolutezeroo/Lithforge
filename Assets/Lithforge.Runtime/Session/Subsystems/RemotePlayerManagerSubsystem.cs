using System;
using System.Collections.Generic;

using Lithforge.Network.Client;
using Lithforge.Network.Server;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class RemotePlayerManagerSubsystem : IGameSubsystem
    {
        private RemotePlayerManager _manager;

        public string Name
        {
            get
            {
                return "RemotePlayerManager";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(NetworkServerSubsystem),
            typeof(NetworkClientSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config is SessionConfig.Client or SessionConfig.Host;
        }

        public void Initialize(SessionContext context)
        {
            // Deferred to PostInitialize — needs arm materials from PlayerSubsystem.
        }

        public void PostInitialize(SessionContext context)
        {
            if (!context.TryGet(out ArmMaterials armMats))
            {
                return;
            }

            Material remoteNameTagMat = new(Shader.Find("UI/Default"));
            _manager = new RemotePlayerManager(
                armMats.Base, armMats.Overlay, remoteNameTagMat);

            context.Register(_manager);

            // Wire based on mode
            if (context.Config is SessionConfig.Client)
            {
                WireClientMode(context);
            }
            else if (context.Config is SessionConfig.Host)
            {
                WireHostMode(context);
            }
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

        private void WireClientMode(SessionContext context)
        {
            if (!context.TryGet(out NetworkClient client))
            {
                return;
            }

            ClientRemotePlayerHandler handler = new(
                _manager, client, client.LocalPlayerId);

            context.Register(handler);

            // Route remote player state messages through ClientWorldSimulation
            if (context.TryGet(out ClientWorldSimulation clientSim))
            {
                clientSim.SetRemotePlayerStateHandler(handler.OnRemotePlayerState);
            }
        }

        private void WireHostMode(SessionContext context)
        {
            if (!context.TryGet(out ServerGameLoop serverGameLoop))
            {
                return;
            }

            RemotePlayerManager mgr = _manager;

            serverGameLoop.OnHostSpawnPlayer = msg =>
            {
                mgr.SpawnPlayer(
                    msg.PlayerId,
                    msg.PlayerName,
                    new float3(msg.PositionX, msg.PositionY, msg.PositionZ),
                    msg.Yaw,
                    msg.Pitch,
                    msg.Flags);
            };

            serverGameLoop.OnHostDespawnPlayer = msg =>
            {
                mgr.DespawnPlayer(msg.PlayerId);
            };

            serverGameLoop.OnHostPlayerState = msg =>
            {
                RemotePlayerSnapshot snapshot = new()
                {
                    Position = new float3(msg.PositionX, msg.PositionY, msg.PositionZ), Yaw = msg.Yaw, Pitch = msg.Pitch, Flags = msg.Flags,
                };
                float serverTimestamp = msg.ServerTick * FixedTickRate.TickDeltaTime;
                mgr.PushSnapshot(msg.PlayerId, serverTimestamp, snapshot);
            };
        }
    }
}
