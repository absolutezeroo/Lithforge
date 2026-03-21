using System;
using System.Diagnostics;
using System.Threading;

using Lithforge.Core.Logging;
using Lithforge.Network.Server;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Owns the background <see cref="System.Threading.Thread" /> that runs
    ///     <see cref="ServerGameLoop.ExecuteOneTick" /> at a fixed 30 TPS rate.
    ///     Call <see cref="Start" /> after all wiring is complete.
    ///     Call <see cref="Dispose" /> to shut down and join the thread.
    /// </summary>
    public sealed class ServerThreadRunner : IDisposable
    {
        /// <summary>Target tick interval in milliseconds (33ms for 30 TPS).</summary>
        private const int TickIntervalMs = 33;

        /// <summary>Maximum time to wait for the thread to join on shutdown.</summary>
        private const int JoinTimeoutMs = 2000;

        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Logger for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>The server game loop whose ExecuteOneTick we call each tick.</summary>
        private readonly ServerGameLoop _serverGameLoop;

        /// <summary>The background thread.</summary>
        private Thread _thread;

        /// <summary>Whether Dispose has been called.</summary>
        private bool _disposed;

        /// <summary>Creates a new runner for the given game loop, bridge, and logger.</summary>
        internal ServerThreadRunner(ServerGameLoop serverGameLoop, ServerThreadBridge bridge, ILogger logger)
        {
            _serverGameLoop = serverGameLoop;
            _bridge = bridge;
            _logger = logger;
        }

        /// <summary>
        ///     Starts the background server thread. Must be called from the main thread
        ///     after all subsystem wiring is complete.
        /// </summary>
        public void Start()
        {
            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "Lithforge.ServerThread",
            };

            _thread.Start();
        }

        /// <summary>
        ///     Checks whether the server thread has faulted and rethrows the exception
        ///     on the calling (main) thread. Call this each frame from <c>GameLoopPoco.Update()</c>.
        /// </summary>
        public void ThrowIfFaulted()
        {
            Exception fault = _bridge.FaultException;

            if (fault is not null)
            {
                throw new ServerThreadFaultException(
                    "Server thread faulted — see inner exception.", fault);
            }
        }

        /// <summary>
        ///     Requests the server thread to stop and waits for it to join.
        ///     Disposes bridge semaphores after the thread has stopped.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _bridge.ShutdownRequested = true;

            // Release any semaphores the server thread might be waiting on
            // to prevent deadlock during shutdown.
            TryReleaseSafe(_bridge.PhysicsResultsReady);
            TryReleaseSafe(_bridge.WorldTickComplete);
            TryReleaseSafe(_bridge.BlockCommandComplete);

            if (_thread is not null && _thread.IsAlive)
            {
                if (!_thread.Join(JoinTimeoutMs))
                {
                    _logger?.LogWarning(
                        "[Lithforge] Server thread did not stop within " + JoinTimeoutMs + "ms.");
                }
            }

            _bridge.Dispose();
        }

        /// <summary>
        ///     The main loop running on the background thread. Calls ExecuteOneTick
        ///     at a fixed rate and sleeps for the remainder of each tick interval.
        /// </summary>
        private void RunLoop()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long nextTickTime = stopwatch.ElapsedMilliseconds;

            try
            {
                while (!_bridge.ShutdownRequested)
                {
                    long now = stopwatch.ElapsedMilliseconds;

                    if (now >= nextTickTime)
                    {
                        _serverGameLoop.ExecuteOneTick(
                            now / 1000f);

                        nextTickTime += TickIntervalMs;

                        // Prevent spiral-of-death: if we fell behind more than 5 ticks, snap forward
                        if (now - nextTickTime > TickIntervalMs * 5)
                        {
                            nextTickTime = now + TickIntervalMs;
                        }
                    }
                    else
                    {
                        long remainingMs = nextTickTime - now;

                        if (remainingMs > 2)
                        {
                            // Sleep for the bulk of the wait, leaving 2ms margin
                            // for spin-wait to handle precisely.
                            Thread.Sleep((int)(remainingMs - 2));
                        }

                        // Spin-wait for the final milliseconds to achieve precise timing.
                        // SpinWait uses Thread.SpinWait internally, escalating to
                        // Thread.Yield / Thread.Sleep(0) after several iterations.
                        SpinWait spinner = new();

                        while (stopwatch.ElapsedMilliseconds < nextTickTime)
                        {
                            spinner.SpinOnce();
                        }
                    }
                }

                // Final save: persist all playing players before the thread exits
                _serverGameLoop.SaveAllPlayers();
            }
            catch (Exception ex)
            {
                // Store the exception for the main thread to rethrow
                _bridge.FaultException = ex;
            }
        }

        /// <summary>
        ///     Safely attempts to release a semaphore without throwing if already at max count.
        /// </summary>
        private static void TryReleaseSafe(SemaphoreSlim semaphore)
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already released — expected during shutdown
            }
            catch (ObjectDisposedException)
            {
                // Already disposed — expected during shutdown
            }
        }
    }
}
