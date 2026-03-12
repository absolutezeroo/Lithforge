using System.Collections.Generic;
using System.Diagnostics;
using Lithforge.Runtime.Debug;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Pipeline;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the generation job queue: scheduling worldgen/storage-load,
    /// polling for completion, running decoration, cross-chunk light
    /// propagation, and disposing resources.
    /// </summary>
    public sealed class GenerationScheduler
    {
        private readonly List<PendingGeneration> _pendingGenerations = new List<PendingGeneration>();
        private readonly List<PendingGeneration> _pendingGenDisposals = new List<PendingGeneration>();
        private readonly ChunkManager _chunkManager;
        private readonly GenerationPipeline _generationPipeline;
        private readonly DecorationStage _decorationStage;
        private readonly WorldStorage _worldStorage;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly long _seed;
        private readonly int _maxGenerationsPerFrame;
        private readonly int _maxGenCompletionsPerFrame;
        private readonly int _maxLightUpdatesPerFrame;
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
        private static readonly int3[] _faceOffsets = new int3[]
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
        private static readonly int[] _oppositeFace = new int[] { 1, 0, 3, 2, 5, 4 };

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

        public int PendingCount
        {
            get { return _pendingGenerations.Count; }
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

        public void PollCompleted()
        {
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
                            _decorationStage.Decorate(
                                pending.Coord,
                                chunk.Data,
                                pending.Handle.HeightMap,
                                pending.Handle.BiomeMap,
                                _seed);
                            long decorEnd = Stopwatch.GetTimestamp();
                            float decorMs = (float)((decorEnd - decorStart) * 1000.0 / Stopwatch.Frequency);
                            PipelineStats.AddDecorate(decorMs);
                        }

                        // Transfer LightData ownership to chunk
                        chunk.LightData = pending.Handle.LightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;
                        PipelineStats.IncrGenCompleted();

                        // Collect border light leaks for cross-chunk propagation
                        CollectBorderLightEntries(chunk, pending.Handle.BorderLightOutput);

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
        }

        /// <summary>
        /// Collects border light entries from the NativeList produced by LightPropagationJob
        /// and stores them in the ManagedChunk for use by neighbor light updates.
        /// </summary>
        private static void CollectBorderLightEntries(
            ManagedChunk chunk,
            NativeList<NativeBorderLightEntry> borderLightOutput)
        {
            chunk.BorderLightEntries.Clear();

            if (!borderLightOutput.IsCreated)
            {
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
        }

        /// <summary>
        /// Checks neighbor chunks and marks them for light re-propagation
        /// if this chunk's border light values would affect them.
        /// </summary>
        private void InvalidateLightNeighbors(int3 coord, ManagedChunk sourceChunk)
        {
            if (sourceChunk.BorderLightEntries.Count == 0)
            {
                return;
            }

            for (int f = 0; f < 6; f++)
            {
                // Check if any border entries exist for this face
                bool hasFaceEntries = false;

                for (int i = 0; i < sourceChunk.BorderLightEntries.Count; i++)
                {
                    if (sourceChunk.BorderLightEntries[i].Face == f)
                    {
                        hasFaceEntries = true;

                        break;
                    }
                }

                if (!hasFaceEntries)
                {
                    continue;
                }

                int3 neighborCoord = coord + _faceOffsets[f];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor != null &&
                    neighbor.State >= ChunkState.RelightPending &&
                    neighbor.LightData.IsCreated)
                {
                    neighbor.NeedsLightUpdate = true;
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
                        chunk.State = ChunkState.Generated;
                    }
                    else if (chunk.State == ChunkState.Meshing)
                    {
                        chunk.NeedsRemesh = true;
                    }

                    entry.SeedEntries.Dispose();
                    chunk.NeedsLightUpdate = false;
                    chunk.LightJobInFlight = false;
                    chunk.ActiveJobHandle = default;

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
                    chunk.NeedsLightUpdate = false;

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
                    int3 neighborCoord = chunk.Coord + _faceOffsets[f];
                    ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                    if (neighbor == null || neighbor.BorderLightEntries.Count == 0)
                    {
                        continue;
                    }

                    int expectedFace = _oppositeFace[f];

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
                    chunk.NeedsLightUpdate = false;
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

        public void ScheduleJobs()
        {
            int slotsAvailable = _maxGenerationsPerFrame - _pendingGenerations.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

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

                    if (_worldStorage.LoadChunk(chunk.Coord, chunk.Data, lightData))
                    {
                        chunk.LightData = lightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;
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
        }

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
                    _inFlightLightUpdates.RemoveAt(i);
                }
            }
        }

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
