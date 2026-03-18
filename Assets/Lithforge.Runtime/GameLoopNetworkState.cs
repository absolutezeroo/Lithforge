using Lithforge.Network.Client;
using Lithforge.Network.Server;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Simulation;

namespace Lithforge.Runtime
{
    /// <summary>
    ///     Groups all network-related state injected into GameLoop.
    ///     Null/default when running in singleplayer mode.
    /// </summary>
    public sealed class GameLoopNetworkState
    {
        public bool IsClientMode { get; set; }

        public ServerGameLoop ServerGameLoop { get; set; }

        public INetworkClient NetworkClient { get; set; }

        public ClientChunkHandler ClientChunkHandler { get; set; }

        public RemotePlayerManager RemotePlayerManager { get; set; }
    }
}
