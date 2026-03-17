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
        [Header("Server")]
        [SerializeField] private ushort serverPort = 25565;
        [SerializeField] private int maxConnections = 200;

        [Header("Client")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string defaultServerAddress = "127.0.0.1";

        public ushort ServerPort
        {
            get { return serverPort; }
        }

        public int MaxConnections
        {
            get { return maxConnections; }
        }

        public string PlayerName
        {
            get { return playerName; }
        }

        public string DefaultServerAddress
        {
            get { return defaultServerAddress; }
        }
    }
}
