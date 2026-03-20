using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Listens for LAN server broadcast packets on a background thread and
    ///     makes discovered servers available to the main thread via
    ///     <see cref="DrainDiscovered" />. Servers that haven't broadcast in 5
    ///     seconds are automatically expired.
    /// </summary>
    public sealed class LanDiscoveryListener : IDisposable
    {
        /// <summary>Seconds after which a server is considered gone.</summary>
        private const float ExpirySeconds = 5f;

        /// <summary>Map of IP address to discovered LAN server entry, maintained on the main thread.</summary>
        private readonly Dictionary<string, LanServerEntry> _discovered = new();

        /// <summary>Logger for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>Thread-safe queue of raw packets received by the background listener thread.</summary>
        private readonly ConcurrentQueue<(byte[] data, string address, DateTime time)> _receiveQueue = new();

        /// <summary>Reusable list for collecting expired entries during the drain sweep (fill pattern).</summary>
        private readonly List<LanServerEntry> _resultCache = new();

        /// <summary>Volatile flag checked by the background thread to signal shutdown.</summary>
        private volatile bool _running;

        /// <summary>Background thread running the listener loop.</summary>
        private Thread _thread;

        /// <summary>UDP socket used for receiving broadcast packets.</summary>
        private UdpClient _udpClient;

        /// <summary>Creates a LAN discovery listener with an optional logger.</summary>
        public LanDiscoveryListener(ILogger logger = null)
        {
            _logger = logger;
        }

        /// <summary>Stops the listener thread, closes the UDP socket, and waits for thread exit.</summary>
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
        ///     Stops the listener thread without blocking. The background thread will
        ///     exit on its next loop iteration. Use this from the main thread to avoid
        ///     the 2-second <see cref="Thread.Join" /> stall that <see cref="Dispose" />
        ///     performs. The discovered entries are cleared.
        /// </summary>
        public void Stop()
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

            _discovered.Clear();
        }

        /// <summary>
        ///     Starts the background listener thread on the LAN discovery port.
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true, Name = "LanDiscoveryListener",
            };
            _thread.Start();
        }

        /// <summary>
        ///     Drains any pending discoveries from the background thread, expires
        ///     old entries, and fills the provided list with all currently known
        ///     LAN servers. Call from the main thread in Update().
        /// </summary>
        public void DrainDiscovered(List<LanServerEntry> results)
        {
            results.Clear();

            // Process new packets
            while (_receiveQueue.TryDequeue(out (byte[] data, string address, DateTime time) item))
            {
                if (LanDiscoveryPacket.TryDeserialize(item.data, item.data.Length, out LanServerInfo info))
                {
                    if (_discovered.TryGetValue(item.address, out LanServerEntry existing))
                    {
                        existing.LastSeen = item.time;
                    }
                    else
                    {
                        _discovered[item.address] = new LanServerEntry(
                            item.address, info, item.time);
                    }
                }
            }

            // Expire old entries and collect results
            _resultCache.Clear();
            DateTime now = DateTime.UtcNow;

            foreach (KeyValuePair<string, LanServerEntry> kvp in _discovered)
            {
                if ((now - kvp.Value.LastSeen).TotalSeconds > ExpirySeconds)
                {
                    _resultCache.Add(kvp.Value);
                }
                else
                {
                    results.Add(kvp.Value);
                }
            }

            for (int i = 0; i < _resultCache.Count; i++)
            {
                _discovered.Remove(_resultCache[i].Address);
            }
        }

        /// <summary>Background thread entry point that receives UDP packets and enqueues them for main-thread processing.</summary>
        private void ListenLoop()
        {
            try
            {
                _udpClient = new UdpClient(LanBroadcaster.DiscoveryPort);
                _udpClient.Client.ReceiveTimeout = 1000;
                IPEndPoint remote = new(IPAddress.Any, 0);

                while (_running)
                {
                    try
                    {
                        byte[] data = _udpClient.Receive(ref remote);
                        _receiveQueue.Enqueue((data, remote.Address.ToString(), DateTime.UtcNow));
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout is expected — just loop and check _running
                    }
                    catch (SocketException ex)
                    {
                        if (_running)
                        {
                            _logger?.LogWarning(
                                $"[LanDiscoveryListener] Receive error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    _logger?.LogError($"[LanDiscoveryListener] Fatal: {ex}");
                }
            }
        }
    }
}
