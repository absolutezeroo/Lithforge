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
        private readonly ChunkManager _chunkManager;
        private readonly GenerationPipeline _generationPipeline;
        private readonly DecorationStage _decorationStage;
        private readonly WorldStorage _worldStorage;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly long _seed;
        private readonly int _maxGenerationsPerFrame;

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
        /// Reusable list for pending light update results — avoids per-frame allocation.
        /// Owner: GenerationScheduler. Lifetime: application.
        /// </summary>
        private readonly List<PendingLightUpdate> _pendingLightUpdates = new List<PendingLightUpdate>();

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
            int maxGenerationsPerFrame)
        {
            _chunkManager = chunkManager;
            _generationPipeline = generationPipeline;
            _decorationStage = decorationStage;
            _worldStorage = worldStorage;
            _nativeStateRegistry = nativeStateRegistry;
            _seed = seed;
            _maxGenerationsPerFrame = maxGenerationsPerFrame;
        }

        public void PollCompleted()
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                PendingGeneration pending = _pendingGenerations[i];

                if (pending.Handle.FinalHandle.IsCompleted)
                {
                    pending.Handle.FinalHandle.Complete();

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
                    _pendingGenerations.RemoveAt(i);
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
        /// Runs synchronously on the main thread for simplicity. Should be called each
        /// frame after PollCompleted().
        /// </summary>
        public void ProcessCrossChunkLightUpdates()
        {
            _chunkManager.FillChunksNeedingLightUpdate(_lightUpdateCache);

            if (_lightUpdateCache.Count == 0)
            {
                return;
            }

            _pendingLightUpdates.Clear();

            // Pass 1: batch-schedule all light update jobs
            for (int c = 0; c < _lightUpdateCache.Count; c++)
            {
                ManagedChunk chunk = _lightUpdateCache[c];

                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    chunk.NeedsLightUpdate = false;

                    continue;
                }

                // Skip chunks with in-flight jobs that may read LightData or ChunkData.
                // The light update will run on a subsequent frame after the job completes.
                if (chunk.State == ChunkState.Meshing ||
                    chunk.State == ChunkState.Generating ||
                    !chunk.ActiveJobHandle.IsCompleted)
                {
                    continue;
                }

                // Build seed entries from all neighbor border light values
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

                        // Map neighbor's border position to this chunk's local coords
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

                    _pendingLightUpdates.Add(new PendingLightUpdate
                    {
                        Chunk = chunk,
                        Handle = handle,
                        SeedEntries = seedEntries,
                    });
                }
                else
                {
                    seedEntries.Dispose();
                    chunk.NeedsLightUpdate = false;
                }
            }

            // Pass 2: single sync point — complete all jobs at once
            if (_pendingLightUpdates.Count > 0)
            {
                NativeArray<JobHandle> handles = new NativeArray<JobHandle>(
                    _pendingLightUpdates.Count, Allocator.Temp);

                for (int i = 0; i < _pendingLightUpdates.Count; i++)
                {
                    handles[i] = _pendingLightUpdates[i].Handle;
                }

                JobHandle.CompleteAll(handles);
                handles.Dispose();

                // Post-completion cleanup
                for (int i = 0; i < _pendingLightUpdates.Count; i++)
                {
                    PendingLightUpdate pending = _pendingLightUpdates[i];
                    ManagedChunk chunk = pending.Chunk;

                    // Mark for remesh since light changed
                    if (chunk.State == ChunkState.Ready)
                    {
                        chunk.State = ChunkState.Generated;
                    }
                    else if (chunk.State == ChunkState.Meshing)
                    {
                        chunk.NeedsRemesh = true;
                    }

                    pending.SeedEntries.Dispose();
                    chunk.NeedsLightUpdate = false;
                }

                _pendingLightUpdates.Clear();
            }
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
            }
        }

        public void CleanupCoord(int3 coord)
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                if (_pendingGenerations[i].Coord.Equals(coord))
                {
                    _pendingGenerations[i].Handle.FinalHandle.Complete();
                    _pendingGenerations[i].Handle.DisposeAll();
                    _pendingCoords.Remove(coord);
                    _pendingGenerations.RemoveAt(i);
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
        }
    }
}
