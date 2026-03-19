using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the LAN broadcaster for advertising the server on the local network.</summary>
    public sealed class LanBroadcasterSubsystem : IGameSubsystem
    {
        /// <summary>The owned LAN broadcaster instance.</summary>
        private LanBroadcaster _broadcaster;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "LanBroadcaster";
            }
        }

        /// <summary>Depends on network server for server port availability.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(NetworkServerSubsystem),
        };

        /// <summary>Only created for host sessions that need LAN discovery.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config is SessionConfig.Host;
        }

        /// <summary>Creates and starts the LAN broadcaster with server info from host config.</summary>
        public void Initialize(SessionContext context)
        {
            SessionConfig.Host host = (SessionConfig.Host)context.Config;
            ContentHash contentHash = ContentHashComputer.Compute(context.Content.StateRegistry);

            _broadcaster = new LanBroadcaster(new LanServerInfo
            {
                serverName = host.DisplayName,
                gamePort = host.ServerPort,
                playerCount = 1,
                maxPlayers = host.MaxPlayers,
                gameVersion = Application.version,
                contentHash = contentHash.ToString(),
                worldName = host.DisplayName,
                gameMode = host.GameMode.ToString(),
            });
            _broadcaster.Start();

            context.Register(_broadcaster);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Stops and disposes the LAN broadcaster.</summary>
        public void Dispose()
        {
            if (_broadcaster != null)
            {
                _broadcaster.Dispose();
                _broadcaster = null;
            }
        }
    }
}
