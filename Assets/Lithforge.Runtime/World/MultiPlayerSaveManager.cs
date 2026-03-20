using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Connection;
using Lithforge.Network.Server;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.World
{
    /// <summary>
    ///     Timer-based save manager for all connected players in multiplayer.
    ///     Periodically captures and persists each player's state via <see cref="PlayerDataStore" />.
    /// </summary>
    public sealed class MultiPlayerSaveManager
    {
        /// <summary>Seconds between periodic save sweeps.</summary>
        private const float SaveInterval = 30f;

        /// <summary>Delegate that captures a peer's current state for persistence.</summary>
        private readonly Func<PeerInfo, WorldPlayerState> _capturer;

        /// <summary>Store for reading/writing player data files.</summary>
        private readonly PlayerDataStore _playerDataStore;

        /// <summary>Network server for iterating connected peers.</summary>
        private readonly NetworkServer _server;

        /// <summary>Realtime timestamp of the last save sweep, or -1 if not yet run.</summary>
        private float _lastSaveTime = -1f;

        /// <summary>Creates the multi-player save manager with the required dependencies.</summary>
        public MultiPlayerSaveManager(
            PlayerDataStore playerDataStore,
            NetworkServer server,
            Func<PeerInfo, WorldPlayerState> capturer)
        {
            _playerDataStore = playerDataStore;
            _server = server;
            _capturer = capturer;
        }

        /// <summary>Checks the timer and saves all playing peers when the interval elapses.</summary>
        public void Tick(float realtimeSinceStartup)
        {
            if (_lastSaveTime < 0f)
            {
                _lastSaveTime = realtimeSinceStartup;
                return;
            }

            if (realtimeSinceStartup < _lastSaveTime + SaveInterval)
            {
                return;
            }

            SaveAll();
            _lastSaveTime = realtimeSinceStartup;
        }

        /// <summary>Saves the state of a single player immediately (e.g. on disconnect).</summary>
        public void SavePlayer(PeerInfo peer)
        {
            if (string.IsNullOrEmpty(peer.PlayerUuid))
            {
                return;
            }

            try
            {
                WorldPlayerState state = _capturer(peer);

                if (state is not null)
                {
                    _playerDataStore.Save(peer.PlayerUuid, state);
                }
            }
            catch (Exception)
            {
                // Best effort — do not crash on save failure
            }
        }

        /// <summary>Saves all currently connected playing peers.</summary>
        public void SaveAll()
        {
            IReadOnlyList<PeerInfo> peers = _server.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];

                if (peer.StateMachine.Current != ConnectionState.Playing)
                {
                    continue;
                }

                SavePlayer(peer);
            }
        }
    }
}
