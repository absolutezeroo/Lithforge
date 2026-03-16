using System.Collections.Generic;
using System.Diagnostics;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Pipeline;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the generation job queue: scheduling worldgen/storage-load,
    /// polling for completion, running decoration, cross-chunk light
    /// propagation, and disposing resources.
    /// </summary>
    public sealed class GenerationScheduler
    {
        /// <summary>Generation jobs currently running on worker threads.</summary>
        private readonly List<PendingGeneration> _pendingGenerations = new List<PendingGeneration>();

        /// <summary>
        /// Generation jobs whose chunk was unloaded while in-flight. Polled each frame
        /// until complete, then all NativeContainers are disposed.
        /// </summary>
        private readonly List<PendingGeneration> _pendingGenDisposals = new List<PendingGeneration>();

        private readonly ChunkManager _chunkManager;
        private readonly GenerationPipeline _generationPipeline;
        private readonly DecorationStage _decorationStage;
        private readonly WorldStorage _worldStorage;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly long _seed;
        private int _maxGenerationsPerFrame;
        private int _maxGenCompletionsPerFrame;
        private readonly int _maxLightUpdatesPerFrame;

        /// <summary>Wall-clock millisecond budget for completion polling each frame.</summary>
        private readonly float _completionBudgetMs;

        /// <summary>
        /// Reusable list for FillChunksToGenerate — avoids per-frame allocation.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _generateCandidateCache = new List<ManagedChunk>();

        /// <summary>
        /// Guards against double-scheduling the same chunk coord.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly HashSet<int3> _pendingCoords = new HashSet<int3>();

        /// <summary>
        /// Face-to-neighbor offset mapping (shared by InvalidateLightNeighbors and
        /// ProcessCrossChunkLightUpdates). Avoids per-call int3[] allocation.
        /// </summary>
        private static readonly int3[] s_faceOffsets = new int3[]
        {
            new int3(1, 0, 0),   // face 0: +X
            new int3(-1, 0, 0),  // face 1: -X
            new int3(0, 1, 0),   // face 2: +Y
            new int3(0, -1, 0),  // face 3: -Y
            new int3(0, 0, 1),   // face 4: +Z
            new int3(0, 0, -1),  // face 5: -Z
        };

        /// <summary>
        /// Opposite face index: face 0 &lt;-&gt; 1, 2 &lt;-&gt; 3, 4 &lt;-&gt; 5.
        /// </summary>
        private static readonly int[] s_oppositeFace = new int[] { 1, 0, 3, 2, 5, 4 };

        /// <summary>
        /// Reusable list for chunks needing light updates — avoids per-frame allocation.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _lightUpdateCache = new List<ManagedChunk>();

        /// <summary>
        /// Reusable list for scheduling light update jobs within ProcessCrossChunkLightUpdates.
        /// Drained into _inFlightLightUpdates at the end of each scheduling pass.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly List<PendingLightUpdate> _pendingLightUpdates = new List<PendingLightUpdate>();

        /// <summary>
        /// Light update jobs scheduled last frame, awaiting completion this frame.
        /// Pattern: schedule frame N, complete frame N+1.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly List<PendingLightUpdate> _inFlightLightUpdates = new List<PendingLightUpdate>();

        /// <summary>
        /// Optional biome tint manager for writing climate data to the global GPU texture.
        /// Set via SetBiomeTintManager after construction.
        /// </summary>
        private BiomeTintManager _biomeTintManager;

        /// <summary>
        /// Optional block entity registry for deserializing block entities from storage.
        /// Set via SetBlockEntityRegistry after construction.
        /// </summary>
        private BlockEntityRegistry _blockEntityRegistry;

        /// <summary>
        /// Called after a chunk with block entities is loaded from storage.
        /// Parameters: chunkCoord, ManagedChunk.
        /// Used by BlockEntityTickScheduler to register entities for ticking.
        /// </summary>
        public System.Action<int3, ManagedChunk> OnChunkEntitiesLoaded;

        /// <summary>Number of generation jobs currently in-flight on worker threads.</summary>
        public int PendingCount
        {
            get { return _pendingGenerations.Count; }
        }

        /// <summary>Injects the biome tint manager so completed chunks can write climate data to the GPU texture.</summary>
        public void SetBiomeTintManager(BiomeTintManager manager)
        {
            _biomeTintManager = manager;
        }

        /// <summary>Injects the block entity registry so chunks loaded from storage can deserialize their entities.</summary>
        public void SetBlockEntityRegistry(BlockEntityRegistry registry)
        {
            _blockEntityRegistry = registry;
        }

        public GenerationScheduler(
            ChunkManager chunkManager,
            GenerationPipeline generationPipeline,
            DecorationStage decorationStage,
            WorldStorage worldStorage,
            NativeStateRegistry nativeStateRegistry,
            long seed,
            int maxGenerationsPerFrame,
            int maxGenCompletionsPerFrame,
            int maxLightUpdatesPerFrame,
            float completionBudgetMs)
        {
            _chunkManager = chunkManager;
            _generationPipeline = generationPipeline;
            _decorationStage = decorationStage;
            _worldStorage = worldStorage;
            _nativeStateRegistry = nativeStateRegistry;
            _seed = seed;
            _maxGenerationsPerFrame = maxGenerationsPerFrame;
            _maxGenCompletionsPerFrame = maxGenCompletionsPerFrame;
            _maxLightUpdatesPerFrame = maxLightUpdatesPerFrame;
            _completionBudgetMs = completionBudgetMs;
        }

        /// <summary>
        /// Re-derives scheduling limits from the current render distance. Called when the
        /// player changes render distance at runtime.
        /// </summary>
        /// <param name="renderDistance">New render distance in chunks.</param>
        public void UpdateConfig(int renderDistance)
        {
            _maxGenerationsPerFrame = SchedulingConfig.MaxGenerationsPerFrame(renderDistance);
            _maxGenCompletionsPerFrame = SchedulingConfig.MaxGenCompletionsPerFrame(renderDistance);
        }

        /// <summary>
        /// Polls in-flight generation jobs for completion and processes finished chunks:
        /// runs decoration, transfers ownership of NativeContainers, collects border light
        /// entries, and marks neighbors for remeshing. Time-budgeted to avoid frame spikes.
        /// </summary>
        public void PollCompleted()
        {
            Profiler.BeginSample("GS.PollCompleted");

            PollPendingGenDisposals();

            FrameBudget budget = new FrameBudget(_completionBudgetMs);
            int completedThisFrame = 0;
            int i = 0;

            while (i < _pendingGenerations.Count)
            {
                if (completedThisFrame >= _maxGenCompletionsPerFrame || budget.IsExhausted())
                {
                    break;
                }

                PendingGeneration pending = _pendingGenerations[i];

                if (pending.Handle.FinalHandle.IsCompleted)
                {
                    completedThisFrame++;
                    long t0 = Stopwatch.GetTimestamp();
                    pending.Handle.FinalHandle.Complete();
                    long t1 = Stopwatch.GetTimestamp();
                    float completeMs = (float)((t1 - t0) * (1000.0 / Stopwatch.Frequency));
                    PipelineStats.RecordGenComplete(completeMs);

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        // Run decoration on main thread (managed code, not Burst)
                        if (_decorationStage != null &&
                            pending.Handle.BiomeMap.IsCreated &&
                            pending.Handle.HeightMap.IsCreated)
                        {
                            long decorStart = Stopwatch.GetTimestamp();
                            Profiler.BeginSample("GS.Decoration");
                            _decorationStage.Decorate(
                                pending.Coord,
                                chunk.Data,
                                pending.Handle.HeightMap,
                                pending.Handle.BiomeMap,
                                pending.Handle.RiverFlags,
                                _seed);
                            Profiler.EndSample();
                            long decorEnd = Stopwatch.GetTimestamp();
                            float decorMs = (float)((decorEnd - decorStart) * 1000.0 / Stopwatch.Frequency);
                            PipelineStats.AddDecorate(decorMs);
                        }

                        // Transfer LightData, HeightMap, and RiverFlags ownership to chunk
                        chunk.LightData = pending.Handle.LightData;
                        chunk.HeightMap = pending.Handle.HeightMap;
                        chunk.RiverFlags = pending.Handle.RiverFlags;
                        _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                        chunk.ActiveJobHandle = default;
                        PipelineStats.IncrGenCompleted();

                        // Collect border light leaks for cross-chunk propagation
                        CollectBorderLightEntries(chunk, pending.Handle.BorderLightOutput);

                        // Write climate data to biome tint texture before disposal
                        if (_biomeTintManager != null && pending.Handle.ClimateMap.IsCreated
                            && pending.Handle.BiomeMap.IsCreated)
                        {
                            _biomeTintManager.WriteChunkClimate(
                                pending.Coord, pending.Handle.ClimateMap, pending.Handle.BiomeMap);
                        }

                        pending.Handle.Dispose();

                        // Invalidate neighbors for remeshing and cross-chunk light
                        _chunkManager.InvalidateReadyNeighbors(pending.Coord);
                        PipelineStats.IncrInvalidate();
                        InvalidateLightNeighbors(pending.Coord, chunk);
                    }
                    else
                    {
                        // Chunk was unloaded — dispose everything including LightData
                        pending.Handle.DisposeAll();
                    }

                    _pendingCoords.Remove(pending.Coord);

                    // Swap-back: O(1) removal instead of O(n) RemoveAt shift
                    int last = _pendingGenerations.Count - 1;

                    if (i < last)
                    {
                        _pendingGenerations[i] = _pendingGenerations[last];
                    }

                    _pendingGenerations.RemoveAt(last);
                    // Do not increment i — recheck position (now holds old last element)
                }
                else
                {
                    i++;
                }
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Collects border light entries from the NativeList produced by LightPropagationJob
        /// and stores them in the ManagedChunk for use by neighbor light updates.
        /// Also rebuilds the BorderFaceMask bitmask for O(1) face-presence checks.
        /// </summary>
        private static void CollectBorderLightEntries(
            ManagedChunk chunk,
            NativeList<NativeBorderLightEntry> borderLightOutput)
        {
            chunk.BorderLightEntries.Clear();

            if (!borderLightOutput.IsCreated)
            {
                chunk.BorderFaceMask = 0;

                return;
            }

            for (int i = 0; i < borderLightOutput.Length; i++)
            {
                NativeBorderLightEntry native = borderLightOutput[i];
                chunk.BorderLightEntries.Add(new BorderLightEntry
                {
                    LocalPosition = native.LocalPosition,
                    PackedLight = native.PackedLight,
                    Face = native.Face,
                });
            }

            ChunkManager.RebuildBorderFaceMask(chunk);
        }

        /// <summary>
        /// Checks neighbor chunks and marks them for light re-propagation
        /// if this chunk's border light values would affect them.
        /// Uses BorderFaceMask for O(1) face-presence check and cached Neighbors[]
        /// to avoid dictionary lookups.
        /// </summary>
        private void InvalidateLightNeighbors(int3 coord, ManagedChunk sourceChunk)
        {
            if (sourceChunk.BorderFaceMask == 0)
            {
                return;
            }

            for (int f = 0; f < 6; f++)
            {
                if ((sourceChunk.BorderFaceMask & (1 << f)) == 0)
                {
                    continue;
                }

                ManagedChunk neighbor = sourceChunk.Neighbors[f];

                if (neighbor != null &&
                    neighbor.State >= ChunkState.RelightPending &&
                    neighbor.LightData.IsCreated)
                {
                    _chunkManager.MarkNeedsLightUpdate(neighbor.Coord);
                }
            }
        }

        /// <summary>
        /// Processes cross-chunk light updates for chunks flagged with NeedsLightUpdate.
        /// Uses a two-phase pattern: complete last frame's jobs, then schedule new ones.
        /// This gives worker threads a full frame (~5-16ms) to finish before the sync point.
        /// Should be called each frame after PollCompleted().
        /// </summary>
        public void ProcessCrossChunkLightUpdates()
        {
            // Phase 1: finalize completed jobs, defer incomplete ones (max 3 frames for TempJob safety)
            for (int i = _inFlightLightUpdates.Count - 1; i >= 0; i--)
            {
                PendingLightUpdate entry = _inFlightLightUpdates[i];

                if (entry.Handle.IsCompleted || entry.FrameAge >= 3)
                {
                    entry.Handle.Complete();
                    ManagedChunk chunk = entry.Chunk;

                    if (chunk.State == ChunkState.Ready)
                    {
                        _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                    }
                    else if (chunk.State == ChunkState.Meshing)
                    {
                        chunk.NeedsRemesh = true;
                    }

                    entry.SeedEntries.Dispose();
                    chunk.LightJobInFlight = false;
                    _chunkManager.NotifyLightJobChanged(chunk);
                    chunk.ActiveJobHandle = default;
                    _chunkManager.ClearNeedsLightUpdate(chunk.Coord);

                    _inFlightLightUpdates.RemoveAt(i);
                }
                else
                {
                    entry.FrameAge++;
                    _inFlightLightUpdates[i] = entry;
                }
            }

            // Phase 2: schedule new light update jobs (completed next frame)
            _chunkManager.FillChunksNeedingLightUpdate(_lightUpdateCache);

            if (_lightUpdateCache.Count == 0)
            {
                return;
            }

            _pendingLightUpdates.Clear();
            int processedCount = 0;

            for (int c = 0; c < _lightUpdateCache.Count; c++)
            {
                if (processedCount >= _maxLightUpdatesPerFrame)
                {
                    break;
                }

                ManagedChunk chunk = _lightUpdateCache[c];

                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    _chunkManager.ClearNeedsLightUpdate(chunk.Coord);

                    continue;
                }

                // Skip chunks with in-flight jobs that may read LightData or ChunkData.
                if (chunk.State == ChunkState.Meshing ||
                    chunk.State == ChunkState.Generating ||
                    !chunk.ActiveJobHandle.IsCompleted)
                {
                    continue;
                }

                NativeList<NativeBorderLightEntry> seedEntries =
                    new NativeList<NativeBorderLightEntry>(128, Allocator.TempJob);

                for (int f = 0; f < 6; f++)
                {
                    ManagedChunk neighbor = chunk.Neighbors[f];

                    if (neighbor == null || neighbor.BorderFaceMask == 0)
                    {
                        continue;
                    }

                    int expectedFace = s_oppositeFace[f];

                    // O(1) bitmask pre-check before iterating entries
                    if ((neighbor.BorderFaceMask & (1 << expectedFace)) == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < neighbor.BorderLightEntries.Count; i++)
                    {
                        BorderLightEntry entry = neighbor.BorderLightEntries[i];

                        if (entry.Face != expectedFace)
                        {
                            continue;
                        }

                        int3 localPos = MapBorderToNeighborLocal(entry.LocalPosition, expectedFace);

                        seedEntries.Add(new NativeBorderLightEntry
                        {
                            LocalPosition = localPos,
                            PackedLight = entry.PackedLight,
                            Face = entry.Face,
                        });
                    }
                }

                if (seedEntries.Length > 0)
                {
                    LightUpdateJob updateJob = new LightUpdateJob
                    {
                        LightData = chunk.LightData,
                        ChunkData = chunk.Data,
                        StateTable = _nativeStateRegistry.States,
                        SeedEntries = seedEntries.AsArray(),
                    };

                    JobHandle handle = updateJob.Schedule();
                    chunk.ActiveJobHandle = handle;
                    chunk.LightJobInFlight = true;
                    _chunkManager.NotifyLightJobChanged(chunk);

                    _pendingLightUpdates.Add(new PendingLightUpdate
                    {
                        Chunk = chunk,
                        Handle = handle,
                        SeedEntries = seedEntries,
                        FrameAge = 0,
                    });

                    processedCount++;
                }
                else
                {
                    seedEntries.Dispose();
                    _chunkManager.ClearNeedsLightUpdate(chunk.Coord);
                }
            }

            // Move scheduled jobs to in-flight list (completed next frame)
            for (int i = 0; i < _pendingLightUpdates.Count; i++)
            {
                _inFlightLightUpdates.Add(_pendingLightUpdates[i]);
            }

            _pendingLightUpdates.Clear();
        }

        /// <summary>
        /// Maps a border voxel's local position from the source chunk to the
        /// receiving chunk's local coordinate system.
        /// </summary>
        private static int3 MapBorderToNeighborLocal(int3 sourceLocal, int sourceFace)
        {
            int lastIdx = ChunkConstants.Size - 1;

            switch (sourceFace)
            {
                case 0: // +X face of source -> -X face of receiver (x=0)
                    return new int3(0, sourceLocal.y, sourceLocal.z);
                case 1: // -X face of source -> +X face of receiver (x=31)
                    return new int3(lastIdx, sourceLocal.y, sourceLocal.z);
                case 2: // +Y face of source -> -Y face of receiver (y=0)
                    return new int3(sourceLocal.x, 0, sourceLocal.z);
                case 3: // -Y face of source -> +Y face of receiver (y=31)
                    return new int3(sourceLocal.x, lastIdx, sourceLocal.z);
                case 4: // +Z face of source -> -Z face of receiver (z=0)
                    return new int3(sourceLocal.x, sourceLocal.y, 0);
                case 5: // -Z face of source -> +Z face of receiver (z=31)
                    return new int3(sourceLocal.x, sourceLocal.y, lastIdx);
                default:
                    return sourceLocal;
            }
        }

        /// <summary>
        /// Fills available scheduling slots with new generation jobs. Tries storage-load
        /// first (instant); if no saved data exists, schedules the full generation pipeline
        /// on worker threads. Calls ScheduleBatchedJobs once at the end to flush.
        /// </summary>
        public void ScheduleJobs()
        {
            int slotsAvailable = _maxGenerationsPerFrame - _pendingGenerations.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            Profiler.BeginSample("GS.ScheduleJobs");

            _chunkManager.FillChunksToGenerate(_generateCandidateCache, slotsAvailable);

            bool scheduled = false;

            for (int i = 0; i < _generateCandidateCache.Count; i++)
            {
                ManagedChunk chunk = _generateCandidateCache[i];

                if (_pendingCoords.Contains(chunk.Coord))
                {
                    continue;
                }

                // Try loading from storage first
                if (_worldStorage != null && _worldStorage.HasChunk(chunk.Coord))
                {
                    NativeArray<byte> lightData = new NativeArray<byte>(
                        ChunkConstants.Volume,
                        Allocator.Persistent, NativeArrayOptions.ClearMemory);

                    if (_worldStorage.LoadChunk(chunk.Coord, chunk.Data, lightData,
                        out Dictionary<int, IBlockEntity> loadedEntities, _blockEntityRegistry))
                    {
                        chunk.LightData = lightData;
                        _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                        chunk.ActiveJobHandle = default;

                        // Attach loaded block entities to the chunk
                        if (loadedEntities != null && loadedEntities.Count > 0)
                        {
                            Dictionary<int, IBlockEntity> chunkEntities =
                                chunk.GetOrCreateBlockEntities();

                            foreach (KeyValuePair<int, IBlockEntity> kvp in loadedEntities)
                            {
                                chunkEntities[kvp.Key] = kvp.Value;
                            }

                            OnChunkEntitiesLoaded?.Invoke(chunk.Coord, chunk);
                        }

                        PipelineStats.IncrGenCompleted();

                        _chunkManager.InvalidateReadyNeighbors(chunk.Coord);
                        PipelineStats.IncrInvalidate();

                        continue;
                    }

                    lightData.Dispose();
                }

                GenerationHandle handle = _generationPipeline.Schedule(chunk.Coord, _seed, chunk.Data);
                chunk.ActiveJobHandle = handle.FinalHandle;

                _pendingGenerations.Add(new PendingGeneration
                {
                    Coord = chunk.Coord,
                    Handle = handle,
                });

                _pendingCoords.Add(chunk.Coord);
                PipelineStats.IncrGenScheduled();
                scheduled = true;
            }

            if (scheduled)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Cancels tracking of a chunk that is being unloaded. Moves in-flight generation
        /// jobs to the deferred disposal queue and force-completes any light update jobs
        /// so that the chunk's NativeContainers can be safely disposed.
        /// </summary>
        /// <param name="coord">Chunk coordinate being unloaded.</param>
        public void CleanupCoord(int3 coord)
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                if (_pendingGenerations[i].Coord.Equals(coord))
                {
                    _pendingCoords.Remove(coord);
                    _pendingGenDisposals.Add(_pendingGenerations[i]);
                    _pendingGenerations.RemoveAt(i);
                }
            }

            // Force-complete in-flight light updates for this coord since the chunk
            // is being unloaded and its LightData/Data will be disposed immediately.
            for (int i = _inFlightLightUpdates.Count - 1; i >= 0; i--)
            {
                if (_inFlightLightUpdates[i].Chunk.Coord.Equals(coord))
                {
                    _inFlightLightUpdates[i].Handle.Complete();
                    _inFlightLightUpdates[i].SeedEntries.Dispose();
                    _inFlightLightUpdates[i].Chunk.LightJobInFlight = false;
                    _chunkManager.NotifyLightJobChanged(_inFlightLightUpdates[i].Chunk);
                    _inFlightLightUpdates.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Force-completes all in-flight jobs and disposes every NativeContainer.
        /// Called during application shutdown or world unload.
        /// </summary>
        public void Shutdown()
        {
            for (int i = 0; i < _pendingGenerations.Count; i++)
            {
                _pendingGenerations[i].Handle.FinalHandle.Complete();
                _pendingGenerations[i].Handle.DisposeAll();
            }

            _pendingGenerations.Clear();
            _pendingCoords.Clear();

            for (int i = 0; i < _pendingGenDisposals.Count; i++)
            {
                _pendingGenDisposals[i].Handle.FinalHandle.Complete();
                _pendingGenDisposals[i].Handle.DisposeAll();
            }

            _pendingGenDisposals.Clear();

            for (int i = 0; i < _inFlightLightUpdates.Count; i++)
            {
                _inFlightLightUpdates[i].Handle.Complete();
                _inFlightLightUpdates[i].SeedEntries.Dispose();
            }

            _inFlightLightUpdates.Clear();
        }

        /// <summary>
        /// Drains the lazy disposal queue for generation jobs. Jobs that were in-flight
        /// when their chunk was unloaded are polled here — once complete, all NativeContainers
        /// (including LightData) are disposed. Called at the start of PollCompleted each frame.
        /// </summary>
        private void PollPendingGenDisposals()
        {
            for (int i = _pendingGenDisposals.Count - 1; i >= 0; i--)
            {
                if (_pendingGenDisposals[i].Handle.FinalHandle.IsCompleted)
                {
                    _pendingGenDisposals[i].Handle.FinalHandle.Complete();
                    _pendingGenDisposals[i].Handle.DisposeAll();
                    _pendingGenDisposals.RemoveAt(i);
                }
            }
        }

        private struct PendingGeneration
        {
            public int3 Coord;
            public GenerationHandle Handle;
        }

        private struct PendingLightUpdate
        {
            public ManagedChunk Chunk;
            public JobHandle Handle;
            public NativeList<NativeBorderLightEntry> SeedEntries;
            public int FrameAge;
        }
    }
}
