using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class LanBroadcasterSubsystem : IGameSubsystem
    {
        private LanBroadcaster _broadcaster;

        public string Name
        {
            get
            {
                return "LanBroadcaster";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(NetworkServerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config is SessionConfig.Host;
        }

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

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
