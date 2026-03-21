using System.Collections.Generic;

using Lithforge.Item;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Manages active container sessions between players and block entities.
    ///     Tracks one active session per player and provides reverse lookup from
    ///     entity key to all viewing sessions (for multi-viewer and destroy cleanup).
    /// </summary>
    public sealed class ServerContainerManager
    {
        /// <summary>Maximum container reach distance squared (6 blocks).</summary>
        private const float MaxDistanceSq = 36f;

        /// <summary>One active container session per player (keyed by player ID).</summary>
        private readonly Dictionary<ushort, ContainerSession> _playerSessions = new();

        /// <summary>
        ///     Reverse lookup from entity key to all sessions viewing that entity.
        ///     Enables efficient multi-viewer notification and destroy cleanup.
        /// </summary>
        private readonly Dictionary<long, List<ContainerSession>> _entityViewers = new();

        /// <summary>Next window ID to assign. Wraps around at 255, skipping 0.</summary>
        private byte _nextWindowId = 1;

        /// <summary>Reusable list for iteration during cleanup.</summary>
        private readonly List<ContainerSession> _tempSessions = new();

        /// <summary>Returns the active container session for a player, or null.</summary>
        public ContainerSession GetSession(ushort playerId)
        {
            _playerSessions.TryGetValue(playerId, out ContainerSession session);
            return session;
        }

        /// <summary>Returns all active sessions. For iteration during broadcast.</summary>
        public IEnumerable<ContainerSession> GetAllSessions()
        {
            return _playerSessions.Values;
        }

        /// <summary>
        ///     Opens a new container session for a player. Closes any existing session first.
        ///     Returns the new session, or null if storage is null.
        /// </summary>
        public ContainerSession OpenSession(
            ushort playerId,
            int3 chunkCoord,
            int flatIndex,
            string entityTypeId,
            IItemStorage storage)
        {
            if (storage is null)
            {
                return null;
            }

            // Close any existing session for this player
            CloseSession(playerId);

            byte windowId = AllocateWindowId();

            ContainerSession session = new()
            {
                WindowId = windowId,
                PlayerId = playerId,
                ChunkCoord = chunkCoord,
                FlatIndex = flatIndex,
                EntityTypeId = entityTypeId,
                Storage = storage,
                RemoteState = new ContainerRemoteState(storage.SlotCount),
                Cursor = ItemStack.Empty,
            };

            _playerSessions[playerId] = session;

            // Add to entity viewers reverse lookup
            long entityKey = session.EntityKey;

            if (!_entityViewers.TryGetValue(entityKey, out List<ContainerSession> viewers))
            {
                viewers = new List<ContainerSession>();
                _entityViewers[entityKey] = viewers;
            }

            viewers.Add(session);

            return session;
        }

        /// <summary>
        ///     Closes the active container session for a player.
        ///     Returns the closed session, or null if none was active.
        /// </summary>
        public ContainerSession CloseSession(ushort playerId)
        {
            if (!_playerSessions.Remove(playerId, out ContainerSession session))
            {
                return null;
            }

            RemoveFromEntityViewers(session);
            return session;
        }

        /// <summary>
        ///     Closes all sessions viewing a specific block entity.
        ///     Returns the list of closed sessions. The caller is responsible for
        ///     returning cursors and sending close messages.
        /// </summary>
        public void CloseAllForEntity(int3 chunkCoord, int flatIndex, List<ContainerSession> result)
        {
            result.Clear();
            long entityKey = ContainerSession.PackEntityKey(chunkCoord, flatIndex);

            if (!_entityViewers.TryGetValue(entityKey, out List<ContainerSession> viewers))
            {
                return;
            }

            // Copy to result before modifying
            for (int i = 0; i < viewers.Count; i++)
            {
                result.Add(viewers[i]);
            }

            // Remove all sessions
            for (int i = 0; i < result.Count; i++)
            {
                _playerSessions.Remove(result[i].PlayerId);
            }

            _entityViewers.Remove(entityKey);
        }

        /// <summary>
        ///     Closes all sessions for a player (used on disconnect).
        ///     Returns the closed session, or null if none was active.
        /// </summary>
        public ContainerSession CloseAllForPlayer(ushort playerId)
        {
            return CloseSession(playerId);
        }

        /// <summary>
        ///     Checks whether a player is within reach of a block entity.
        ///     Uses Chebyshev-style squared distance check.
        /// </summary>
        public static bool IsWithinReach(float3 playerPos, int3 blockWorldPos)
        {
            float dx = playerPos.x - (blockWorldPos.x + 0.5f);
            float dy = playerPos.y - (blockWorldPos.y + 0.5f);
            float dz = playerPos.z - (blockWorldPos.z + 0.5f);
            return dx * dx + dy * dy + dz * dz <= MaxDistanceSq;
        }

        /// <summary>Removes a session from the entity viewers reverse lookup.</summary>
        private void RemoveFromEntityViewers(ContainerSession session)
        {
            long entityKey = session.EntityKey;

            if (!_entityViewers.TryGetValue(entityKey, out List<ContainerSession> viewers))
            {
                return;
            }

            for (int i = viewers.Count - 1; i >= 0; i--)
            {
                if (viewers[i].PlayerId == session.PlayerId)
                {
                    viewers.RemoveAt(i);
                    break;
                }
            }

            if (viewers.Count == 0)
            {
                _entityViewers.Remove(entityKey);
            }
        }

        /// <summary>Allocates the next available window ID, skipping 0.</summary>
        private byte AllocateWindowId()
        {
            byte id = _nextWindowId;
            _nextWindowId++;

            if (_nextWindowId == 0)
            {
                _nextWindowId = 1;
            }

            return id;
        }
    }
}
