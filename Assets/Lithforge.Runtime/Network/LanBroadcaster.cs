using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Broadcasts <see cref="LanServerInfo" /> via UDP on the LAN every 1.5 seconds.
    ///     Used by Host and DedicatedServer modes to advertise to clients running
    ///     <see cref="LanDiscoveryListener" />. Runs on a dedicated background thread.
    /// </summary>
    public sealed class LanBroadcaster : IDisposable
    {
        /// <summary>Fixed discovery port separate from the game port.</summary>
        public const ushort DiscoveryPort = 47777;

        /// <summary>Broadcast interval in milliseconds.</summary>
        private const int BroadcastIntervalMs = 1500;

        private readonly LanServerInfo _info;

        private readonly byte[] _packetBuffer = new byte[LanDiscoveryPacket.MaxPacketSize];

        private volatile bool _running;

        private Thread _thread;

        private UdpClient _udpClient;

        public LanBroadcaster(LanServerInfo info)
        {
            _info = info;
        }

        public void Dispose()
        {
            _running = false;

            try
            {
                _udpClient?.Close();
            }
            catch (Exception)
            {
                // Ignore close errors during shutdown
            }

            _udpClient = null;

            if (_thread is
                {
                    IsAlive: true,
                })
            {
                _thread.Join(2000);
            }

            _thread = null;
        }

        /// <summary>
        ///     Starts the background broadcast thread. Safe to call multiple times
        ///     (subsequent calls are no-ops while running).
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(BroadcastLoop)
            {
                IsBackground = true, Name = "LanBroadcaster",
            };
            _thread.Start();
        }

        /// <summary>
        ///     Updates the player count in the broadcast payload.
        ///     Thread-safe: reads from the broadcast thread use the latest value.
        /// </summary>
        public void UpdatePlayerCount(int count)
        {
            _info.playerCount = count;
        }

        private void BroadcastLoop()
        {
            try
            {
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                IPEndPoint endpoint = new(IPAddress.Broadcast, DiscoveryPort);

                while (_running)
                {
                    try
                    {
                        int length = LanDiscoveryPacket.Serialize(_info, _packetBuffer);

                        if (length > 0)
                        {
                            _udpClient.Send(_packetBuffer, length, endpoint);
                        }
                    }
                    catch (SocketException ex)
                    {
                        UnityEngine.Debug.LogWarning($"[LanBroadcaster] Send failed: {ex.Message}");
                    }

                    Thread.Sleep(BroadcastIntervalMs);
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    UnityEngine.Debug.LogError($"[LanBroadcaster] Fatal: {ex}");
                }
            }
        }
    }
}
