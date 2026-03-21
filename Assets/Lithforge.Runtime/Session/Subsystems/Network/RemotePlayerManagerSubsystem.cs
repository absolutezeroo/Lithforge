using System;
using System.Collections.Generic;

using Lithforge.Runtime.Player;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the remote player manager for rendering other players in multiplayer.</summary>
    public sealed class RemotePlayerManagerSubsystem : IGameSubsystem
    {
        /// <summary>The owned remote player manager instance.</summary>
        private RemotePlayerManager _manager;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "RemotePlayerManager";
            }
        }

        /// <summary>Depends on player and network subsystems for materials and connectivity.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(NetworkServerSubsystem),
            typeof(NetworkClientSubsystem),
        };

        /// <summary>Created for all rendering modes (SP for Open-to-LAN readiness, Host, Client).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // All rendering modes: SP needs it for Open-to-LAN readiness
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.Client;
        }

        /// <summary>Deferred to PostInitialize; needs arm materials from PlayerSubsystem.</summary>
        public void Initialize(SessionContext context)
        {
            // Deferred to PostInitialize — needs arm materials from PlayerSubsystem.
        }

        /// <summary>Creates the remote player manager with arm materials for player model rendering.</summary>
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

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the remote player manager and all player entities.</summary>
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
