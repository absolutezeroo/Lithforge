using System;
using System.Collections.Generic;

using Lithforge.Runtime.Player;
using Lithforge.Runtime.World;

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
            // All rendering modes: SP needs it for Open-to-LAN readiness
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.Client;
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

            // ClientRemotePlayerHandler is created by NetworkClientSubsystem's
            // OnHandshakeComplete callback, after the server assigns a player ID.
            // This ensures the handler has the correct LocalPlayerId for filtering.
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
