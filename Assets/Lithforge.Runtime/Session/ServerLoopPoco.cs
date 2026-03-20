using System;
using System.Collections.Generic;

using Lithforge.Network.Bridge;

using Unity.Mathematics;

using UnityEngine.Profiling;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Encapsulates server-side per-frame work: generation polling, chunk loading/unloading,
    ///     job scheduling, and gameplay ticking. Used in singleplayer and host modes.
    ///     Extracted from <see cref="GameLoopPoco" /> so client mode can skip these entirely.
    /// </summary>
    public sealed class ServerLoopPoco
    {
        /// <summary>Configuration bag with all server-side dependencies.</summary>
        private readonly ServerLoopConfig _config;

        /// <summary>Reusable list of coords unloaded during the current frame.</summary>
        private readonly List<int3> _unloadedCoords = new();

        /// <summary>Creates a new server loop with the given config.</summary>
        public ServerLoopPoco(ServerLoopConfig config)
        {
            _config = config;
        }

        /// <summary>Coords unloaded during the last UpdateLoadingAndUnloading call.</summary>
        public IReadOnlyList<int3> UnloadedCoords
        {
            get { return _unloadedCoords; }
        }

        /// <summary>Number of chunks awaiting generation job completion.</summary>
        public int PendingGenerationCount
        {
            get { return _config.GenerationScheduler?.PendingCount ?? 0; }
        }

        /// <summary>
        ///     Polls completed generation jobs, transitioning chunks from Generating to Generated.
        /// </summary>
        public void PollCompletions()
        {
            Profiler.BeginSample("SL.PollGen");
            _config.GenerationScheduler?.PollCompleted();
            Profiler.EndSample();
        }

        /// <summary>
        ///     Drives the chunk loading queue and unloads distant chunks.
        ///     Populates <see cref="UnloadedCoords" /> with any coords that were unloaded.
        /// </summary>
        public void UpdateLoadingAndUnloading()
        {
            _unloadedCoords.Clear();

            PlayerChunkSnapshot snapshot = _config.GetPlayerChunkSnapshot();
            double realtime = _config.GetCurrentRealtime();
            float3 lookAhead = _config.GetServerLookAhead();

            ReadOnlySpan<int3> playerCoords = snapshot.Count > 0
                ? new ReadOnlySpan<int3>(snapshot.Coords, 0, snapshot.Count)
                : ReadOnlySpan<int3>.Empty;

            Profiler.BeginSample("SL.LoadQueue");
            _config.ChunkManager.UpdateLoadingQueue(playerCoords, lookAhead);
            Profiler.EndSample();

            Profiler.BeginSample("SL.RefCounts");
            _config.ChunkManager.AdjustRefCounts(
                playerCoords, realtime, _config.GracePeriodSeconds);
            Profiler.EndSample();

            Profiler.BeginSample("SL.Unload");
            _config.ChunkManager.UnloadDistantChunks(
                playerCoords, _unloadedCoords, realtime,
                _config.WorldStorage, _config.UnloadBudgetMs,
                _config.GeneratedChunkCache);
            Profiler.EndSample();
        }

        /// <summary>
        ///     Schedules generation and cross-chunk light update jobs.
        /// </summary>
        public void ScheduleJobs()
        {
            Profiler.BeginSample("SL.SchedGen");
            _config.GenerationScheduler?.ScheduleJobs();
            Profiler.EndSample();

            Profiler.BeginSample("SL.CrossLight");
            _config.GenerationScheduler?.ProcessCrossChunkLightUpdates();
            Profiler.EndSample();
        }

        /// <summary>
        ///     Ticks gameplay systems: spawn manager, auto-save.
        /// </summary>
        public void TickGameplay(float realtime)
        {
            _config.AutoSaveManager?.Tick(realtime);
        }

        /// <summary>
        ///     Propagates a render distance change to the generation scheduler.
        /// </summary>
        public void NotifyRenderDistanceChanged(int renderDistance)
        {
            _config.GenerationScheduler?.UpdateConfig(renderDistance);
        }

        /// <summary>
        ///     Cleans up generation scheduler state for an unloaded chunk coordinate.
        ///     Prevents zombie generation slots from blocking new chunk generation.
        /// </summary>
        public void CleanupGenerationCoord(int3 coord)
        {
            _config.GenerationScheduler?.CleanupCoord(coord);
        }

        /// <summary>
        ///     Completes all in-flight generation and liquid jobs before shutdown.
        /// </summary>
        public void Shutdown()
        {
            _config.LiquidScheduler?.Shutdown();
            _config.GenerationScheduler?.Shutdown();
        }
    }
}
