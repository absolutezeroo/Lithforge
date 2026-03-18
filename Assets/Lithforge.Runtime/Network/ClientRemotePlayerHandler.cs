using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Client-side handler for remote player lifecycle messages. Registers on the
    ///     client's <see cref="MessageDispatcher" /> for SpawnPlayer and DespawnPlayer.
    ///     Routes spawn/despawn to <see cref="RemotePlayerManager" />.
    ///     Remote player state (position/rotation) is routed through
    ///     <see cref="ClientWorldSimulation.SetRemotePlayerStateHandler" /> because
    ///     <see cref="MessageDispatcher" /> supports only one handler per message type,
    ///     and PlayerState is already registered for client-side prediction reconciliation.
    /// </summary>
    public sealed class ClientRemotePlayerHandler
    {
        private readonly ushort _localPlayerId;
        private readonly RemotePlayerManager _remotePlayerManager;

        public ClientRemotePlayerHandler(
            RemotePlayerManager remotePlayerManager,
            INetworkClient networkClient,
            ushort localPlayerId)
        {
            _remotePlayerManager = remotePlayerManager;
            _localPlayerId = localPlayerId;

            MessageDispatcher dispatcher = networkClient.Dispatcher;
            dispatcher.RegisterHandler(MessageType.SpawnPlayer, OnSpawnPlayer);
            dispatcher.RegisterHandler(MessageType.DespawnPlayer, OnDespawnPlayer);
        }

        /// <summary>
        ///     Called by <see cref="ClientWorldSimulation" /> when a PlayerStateMessage
        ///     arrives for a remote player (PlayerId != localPlayerId).
        ///     Converts to a <see cref="RemotePlayerSnapshot" /> and pushes into the
        ///     entity's interpolation buffer.
        /// </summary>
        public void OnRemotePlayerState(PlayerStateMessage msg)
        {
            RemotePlayerSnapshot snapshot = new()
            {
                Position = new float3(msg.PositionX, msg.PositionY, msg.PositionZ), Yaw = msg.Yaw, Pitch = msg.Pitch, Flags = msg.Flags,
            };

            // Use ServerTick as the timestamp for interpolation
            float serverTimestamp = msg.ServerTick * FixedTickRate.TickDeltaTime;
            _remotePlayerManager.PushSnapshot(msg.PlayerId, serverTimestamp, snapshot);
        }

        private void OnSpawnPlayer(ConnectionId connId, byte[] data, int offset, int length)
        {
            SpawnPlayerMessage msg = SpawnPlayerMessage.Deserialize(data, offset, length);

            // Never spawn ourselves as a remote player
            if (msg.PlayerId == _localPlayerId)
            {
                return;
            }

            float3 position = new(msg.PositionX, msg.PositionY, msg.PositionZ);

            _remotePlayerManager.SpawnPlayer(
                msg.PlayerId,
                msg.PlayerName,
                position,
                msg.Yaw,
                msg.Pitch,
                msg.Flags);
        }

        private void OnDespawnPlayer(ConnectionId connId, byte[] data, int offset, int length)
        {
            DespawnPlayerMessage msg = DespawnPlayerMessage.Deserialize(data, offset, length);
            _remotePlayerManager.DespawnPlayer(msg.PlayerId);
        }
    }
}
