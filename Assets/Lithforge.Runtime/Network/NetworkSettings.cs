using UnityEngine;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// ScriptableObject holding network configuration.
    /// Lives in Assets/Resources/Settings/NetworkSettings.asset.
    /// Loaded by ContentPipeline or LithforgeBootstrap at startup.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkSettings", menuName = "Lithforge/Settings/Network Settings")]
    public sealed class NetworkSettings : ScriptableObject
    {
        /// <summary>Server listening port for UTP game connections.</summary>
        [Header("Server")]
        [SerializeField] private ushort serverPort = 25565;

        /// <summary>Maximum number of concurrent client connections.</summary>
        [SerializeField] private int maxConnections = 200;

        /// <summary>Default player display name for new clients.</summary>
        [Header("Client")]
        [SerializeField] private string playerName = "Player";

        /// <summary>Default server address pre-filled in the join game screen.</summary>
        [SerializeField] private string defaultServerAddress = "127.0.0.1";

        /// <summary>Server listening port for UTP game connections.</summary>
        public ushort ServerPort
        {
            get { return serverPort; }
        }

        /// <summary>Maximum number of concurrent client connections.</summary>
        public int MaxConnections
        {
            get { return maxConnections; }
        }

        /// <summary>Default player display name for new clients.</summary>
        public string PlayerName
        {
            get { return playerName; }
        }

        /// <summary>Default server address pre-filled in the join game screen.</summary>
        public string DefaultServerAddress
        {
            get { return defaultServerAddress; }
        }
    }
}
