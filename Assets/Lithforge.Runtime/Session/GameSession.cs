using System;
using System.Collections.Generic;

using Lithforge.Runtime.Bootstrap;
using Lithforge.Runtime.World;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Owns all session-lifetime subsystems for one game session.
    ///     Creates subsystems via <see cref="BuildSubsystemList" />, filters by
    ///     <see cref="IGameSubsystem.ShouldCreate" />, sorts topologically,
    ///     and initializes in order. Disposal runs in reverse via
    ///     <see cref="SessionLifetimeTracker" />.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        /// <summary>All initialized subsystems in topological order.</summary>
        private readonly List<IGameSubsystem> _subsystems = new();

        /// <summary>Whether this session has already been disposed.</summary>
        private bool _disposed;

        /// <summary>Whether all subsystems have been initialized and post-initialized.</summary>
        private bool _initialized;

        /// <summary>Logger for session lifecycle diagnostics.</summary>
        private ILogger _logger;

        /// <summary>The session context containing config, services, and lifetime tracker.</summary>
        public SessionContext Context { get; private set; }

        /// <summary>
        ///     Disposes all subsystems in reverse initialization order.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ShutdownAndDispose();
        }

        /// <summary>
        ///     Creates and initializes all subsystems for this session.
        /// </summary>
        public void Initialize(
            SessionConfig config,
            AppContext app,
            ContentPipelineResult content)
        {
            _logger = app.Logger;
            SessionLifetimeTracker lifetime = new(_logger);
            Context = new SessionContext(config, app, content, lifetime);

            // Build the full candidate list
            List<IGameSubsystem> candidates = new();
            BuildSubsystemList(candidates);

            // Filter by ShouldCreate
            List<IGameSubsystem> filtered = new(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].ShouldCreate(config))
                {
                    filtered.Add(candidates[i]);
                }
            }

            // Topological sort
            List<IGameSubsystem> sorted = SubsystemTopologicalSorter.Sort(filtered);

            // Initialize in order
            for (int i = 0; i < sorted.Count; i++)
            {
                IGameSubsystem sub = sorted[i];

                try
                {
                    sub.Initialize(Context);
                    _subsystems.Add(sub);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"[Lithforge] Failed to initialize {sub.Name}: {ex}");

                    // Dispose already-initialized subsystems in reverse order
                    ShutdownAndDispose();
                    throw;
                }
            }

            // PostInitialize in the same order
            for (int i = 0; i < _subsystems.Count; i++)
            {
                IGameSubsystem sub = _subsystems[i];

                try
                {
                    sub.PostInitialize(Context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"[Lithforge] Failed to post-initialize {sub.Name}: {ex}");
                    ShutdownAndDispose();
                    throw;
                }
            }

            _initialized = true;
        }

        /// <summary>
        ///     Shuts down all schedulers (completes Burst jobs) without disposing.
        ///     Called before save operations during quit-to-title.
        /// </summary>
        public void Shutdown()
        {
            // Shutdown in reverse order (most-dependent first)
            for (int i = _subsystems.Count - 1; i >= 0; i--)
            {
                try
                {
                    _subsystems[i].Shutdown();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        $"[Lithforge] Error shutting down {_subsystems[i].Name}: {ex}");
                }
            }
        }

        /// <summary>
        ///     Completes in-flight Burst jobs then disposes all subsystems in reverse order.
        /// </summary>
        private void ShutdownAndDispose()
        {
            // Complete any in-flight Burst jobs before disposing NativeContainers.
            // Even if Shutdown() was already called (e.g., by SessionOrchestrator),
            // new jobs may have been scheduled during save coroutine yields.
            // Shutdown() is idempotent — completing already-completed jobs is a no-op.
            Shutdown();

            // Dispose subsystems in reverse order
            for (int i = _subsystems.Count - 1; i >= 0; i--)
            {
                try
                {
                    _subsystems[i].Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        $"[Lithforge] Error disposing {_subsystems[i].Name}: {ex}");
                }
            }

            _subsystems.Clear();

            // Dispose the lifetime tracker (handles any tracked IDisposables)
            Context?.Lifetime?.Dispose();
        }

        /// <summary>
        ///     Populates the candidate list with all known subsystem types.
        ///     Order does not matter — topological sort handles initialization order.
        /// </summary>
        private static void BuildSubsystemList(List<IGameSubsystem> list)
        {
            // Phase 1: Storage & chunk infrastructure
            list.Add(new Subsystems.SessionLockSubsystem());
            list.Add(new Subsystems.StorageSubsystem());
            list.Add(new Subsystems.ChunkPoolSubsystem());
            list.Add(new Subsystems.ChunkManagerSubsystem());
            list.Add(new Subsystems.ChunkPersistenceSubsystem());

            // Phase 2: World generation
            list.Add(new Subsystems.WorldGenSubsystem());
            list.Add(new Subsystems.DecorationSubsystem());

            // Phase 3: Rendering
            list.Add(new Subsystems.MaterialSubsystem());
            list.Add(new Subsystems.GpuBufferResizerSubsystem());
            list.Add(new Subsystems.ChunkMeshStoreSubsystem());
            list.Add(new Subsystems.BiomeTintSubsystem());

            // Phase 4: Schedulers
            list.Add(new Subsystems.GenerationSchedulerSubsystem());
            list.Add(new Subsystems.RelightSchedulerSubsystem());
            list.Add(new Subsystems.MeshSchedulerSubsystem());
            list.Add(new Subsystems.LODSchedulerSubsystem());
            list.Add(new Subsystems.LiquidSubsystem());

            // Phase 5: Simulation
            list.Add(new Subsystems.InputSubsystem());
            list.Add(new Subsystems.TickRegistrySubsystem());
            list.Add(new Subsystems.WorldSimulationSubsystem());

            // Phase 6: Networking
            list.Add(new Subsystems.NetworkServerSubsystem());
            list.Add(new Subsystems.LanBroadcasterSubsystem());
            list.Add(new Subsystems.NetworkClientSubsystem());
            list.Add(new Subsystems.ClientChunkHandlerSubsystem());
            list.Add(new Subsystems.RemotePlayerManagerSubsystem());

            // Phase 7: Player & gameplay
            list.Add(new Subsystems.PlayerDataSubsystem());
            list.Add(new Subsystems.PlayerSubsystem());
            list.Add(new Subsystems.BlockEntitySubsystem());
            list.Add(new Subsystems.BlockInteractionSubsystem());
            list.Add(new Subsystems.AutoSaveSubsystem());
            list.Add(new Subsystems.SpawnSubsystem());
            list.Add(new Subsystems.TimeOfDaySubsystem());

            // Phase 8: UI
            list.Add(new Subsystems.HudSubsystem());
            list.Add(new Subsystems.ContainerScreenSubsystem());
            list.Add(new Subsystems.SettingsScreenSubsystem());
            list.Add(new Subsystems.PauseMenuSubsystem());

            // Phase 9: Debug & diagnostics
            list.Add(new Subsystems.MetricsSubsystem());
            list.Add(new Subsystems.F3OverlaySubsystem());
            list.Add(new Subsystems.BenchmarkSubsystem());

            // Phase 10: Audio
            list.Add(new Subsystems.AudioSubsystem());

            // Phase 11: Bridge (must be last)
            list.Add(new Subsystems.SessionBridgeSubsystem());
        }
    }
}
