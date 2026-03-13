using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the relight job queue: scheduling LightRemovalJobs for RelightPending chunks,
    /// polling for completion, comparing border light entries for cross-chunk cascade,
    /// and managing border entry snapshots. Extracted from MeshScheduler to separate
    /// relighting concerns from meshing concerns.
    /// Two-phase pattern: schedule frame N, complete frame N+1.
    /// </summary>
    public sealed class RelightScheduler
    {
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly int _maxRelightsPerFrame;

        /// <summary>
        /// Reusable list for FillChunksNeedingRelight — avoids per-frame allocation.
        /// Owner: RelightScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _relightCache = new List<ManagedChunk>();

        /// <summary>
        /// Relight jobs in flight, awaiting completion next frame.
        /// Two-phase pattern: schedule frame N, complete frame N+1.
        /// Owner: RelightScheduler. Lifetime: application.
        /// </summary>
        private readonly List<PendingRelight> _inFlightRelights = new List<PendingRelight>();

        /// <summary>
        /// Pool for List&lt;BorderLightEntry&gt; snapshots used during relight.
        /// Avoids per-relight allocation of the old border entry snapshot list.
        /// Owner: RelightScheduler. Lifetime: application.
        /// </summary>
        private readonly Stack<List<BorderLightEntry>> _borderEntryListPool = new Stack<List<BorderLightEntry>>();

        /// <summary>
        /// Reusable flat array for O(1) old-border-entry lookup in ProcessBorderCascade.
        /// Index: flat voxel index (0..Volume-1). Value: packed light byte (0 = not present).
        /// PackedLight is never 0 for border entries (filtered by sun > 1 || block > 1),
        /// so 0 is a safe sentinel for "no old entry at this position".
        /// Cleared and rebuilt each call — no stale data risk.
        /// Owner: RelightScheduler. Lifetime: application.
        /// </summary>
        private readonly byte[] _oldBorderLookup = new byte[ChunkConstants.Volume];

        /// <summary>
        /// Face offsets for 6 cardinal directions: +X, -X, +Y, -Y, +Z, -Z.
        /// </summary>
        private static readonly int3[] _faceOffsets =
        {
            new int3(1, 0, 0),   // 0: +X
            new int3(-1, 0, 0),  // 1: -X
            new int3(0, 1, 0),   // 2: +Y
            new int3(0, -1, 0),  // 3: -Y
            new int3(0, 0, 1),   // 4: +Z
            new int3(0, 0, -1),  // 5: -Z
        };

        /// <summary>
        /// Maps face index to the opposite face: +X(0)↔-X(1), +Y(2)↔-Y(3), +Z(4)↔-Z(5).
        /// </summary>
        private static readonly int[] _oppositeFace = { 1, 0, 3, 2, 5, 4 };

        /// <summary>
        /// Number of cascades triggered this frame. Reset each PollCompleted call.
        /// Used to spread border cascades across frames.
        /// </summary>
        private int _cascadesThisFrame;

        private const int _maxCascadesPerFrame = 12;

        public RelightScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            int maxRelightsPerFrame = 6)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _maxRelightsPerFrame = maxRelightsPerFrame;
        }

        /// <summary>
        /// Schedules async relight jobs for RelightPending chunks.
        /// Two-phase pattern: jobs are kicked to worker threads here and completed
        /// in PollCompleted() next frame. Chunks stay in RelightPending state
        /// until the relight job completes, which gates meshing automatically (since
        /// FillChunksToMesh only returns Generated chunks).
        /// Uses targeted changed indices from PendingEditIndices and border removal
        /// seeds from PendingBorderRemovals instead of scanning all 32768 voxels.
        /// </summary>
        public void ScheduleJobs()
        {
            Profiler.BeginSample("RS.ScheduleJobs");
            _chunkManager.FillChunksNeedingRelight(_relightCache);

            if (_relightCache.Count == 0)
            {
                Profiler.EndSample();
                return;
            }

            if (_relightCache.Count > 20)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RelightScheduler] {_relightCache.Count} chunks pending relight — possible cascade loop");
            }

            bool scheduled = false;
            int scheduledCount = 0;

            for (int i = 0; i < _relightCache.Count && scheduledCount < _maxRelightsPerFrame; i++)
            {
                ManagedChunk chunk = _relightCache[i];

                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    chunk.State = ChunkState.Generated;
                    continue;
                }

                if (IsRelightInFlight(chunk))
                {
                    continue;
                }

                // Build targeted changed indices from pending edits
                NativeArray<int> changedIndices = new NativeArray<int>(
                    chunk.PendingEditIndices.Count, Allocator.Persistent);

                for (int j = 0; j < chunk.PendingEditIndices.Count; j++)
                {
                    changedIndices[j] = chunk.PendingEditIndices[j];
                }

                chunk.PendingEditIndices.Clear();

                // Build border removal seeds from pending cross-chunk cascade
                NativeArray<NativeBorderLightEntry> borderRemovalSeeds =
                    new NativeArray<NativeBorderLightEntry>(
                        chunk.PendingBorderRemovals.Count, Allocator.Persistent);

                for (int j = 0; j < chunk.PendingBorderRemovals.Count; j++)
                {
                    BorderLightEntry entry = chunk.PendingBorderRemovals[j];
                    borderRemovalSeeds[j] = new NativeBorderLightEntry
                    {
                        LocalPosition = entry.LocalPosition,
                        PackedLight = entry.PackedLight,
                        Face = entry.Face,
                    };
                }

                chunk.PendingBorderRemovals.Clear();

                // Allocate border light output for post-relight border scanning
                NativeList<NativeBorderLightEntry> borderLightOutput =
                    new NativeList<NativeBorderLightEntry>(256, Allocator.Persistent);

                // Snapshot old border entries for diffing after job completes
                List<BorderLightEntry> oldBorderEntries;

                if (_borderEntryListPool.Count > 0)
                {
                    oldBorderEntries = _borderEntryListPool.Pop();
                    oldBorderEntries.Clear();
                }
                else
                {
                    oldBorderEntries = new List<BorderLightEntry>();
                }

                oldBorderEntries.AddRange(chunk.BorderLightEntries);

                LightRemovalJob removalJob = new LightRemovalJob
                {
                    LightData = chunk.LightData,
                    ChunkData = chunk.Data,
                    StateTable = _nativeStateRegistry.States,
                    HeightMap = chunk.HeightMap,
                    ChunkWorldY = chunk.Coord.y * ChunkConstants.Size,
                    ChangedIndices = changedIndices,
                    BorderRemovalSeeds = borderRemovalSeeds,
                    BorderLightOutput = borderLightOutput,
                };

                JobHandle handle = removalJob.Schedule();

                chunk.ActiveJobHandle = handle;
                chunk.LightJobInFlight = true;

                _inFlightRelights.Add(new PendingRelight
                {
                    Chunk = chunk,
                    Handle = handle,
                    FrameAge = 0,
                    ChangedIndices = changedIndices,
                    BorderRemovalSeeds = borderRemovalSeeds,
                    BorderLightOutput = borderLightOutput,
                    OldBorderEntries = oldBorderEntries,
                });

                scheduled = true;
                scheduledCount++;
            }

            if (scheduled)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Completes in-flight relight jobs from last frame. Jobs that completed on
        /// worker threads transition their chunk from RelightPending to Generated.
        /// After completion, compares old vs new border light entries to cascade
        /// light changes to neighboring chunks (both removal and increase paths).
        /// Allocations use Persistent so there is no TempJob 4-frame deadline.
        /// FrameAge > 300 (~5 seconds) is a safety net for stuck jobs only.
        /// </summary>
        public void PollCompleted()
        {
            _cascadesThisFrame = 0;

            for (int i = _inFlightRelights.Count - 1; i >= 0; i--)
            {
                PendingRelight entry = _inFlightRelights[i];

                if ((entry.FrameAge >= 4 && entry.Handle.IsCompleted) || entry.FrameAge > 300)
                {
                    if (entry.FrameAge > 300)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[RelightScheduler] Force-completing stale relight job for chunk " +
                            $"{entry.Chunk.Coord} after {entry.FrameAge} frames");
                    }

                    entry.Handle.Complete();

                    // Process border cascade before transitioning state
                    ProcessBorderCascade(entry);

                    entry.Chunk.LightJobInFlight = false;
                    entry.Chunk.ActiveJobHandle = default;

                    // If border removal seeds were accumulated while this job was in
                    // flight (from a neighbor's cascade), immediately re-enter
                    // RelightPending so they are processed next frame.
                    if (entry.Chunk.PendingBorderRemovals.Count > 0 ||
                        entry.Chunk.PendingEditIndices.Count > 0)
                    {
                        entry.Chunk.State = ChunkState.RelightPending;
                    }
                    else
                    {
                        entry.Chunk.State = ChunkState.Generated;
                    }

                    // Dispose native containers from this relight
                    entry.ChangedIndices.Dispose();
                    entry.BorderRemovalSeeds.Dispose();
                    entry.BorderLightOutput.Dispose();

                    // Return pooled list for reuse
                    _borderEntryListPool.Push(entry.OldBorderEntries);

                    _inFlightRelights.RemoveAt(i);
                }
                else
                {
                    entry.FrameAge++;
                    _inFlightRelights[i] = entry;
                }
            }
        }

        /// <summary>
        /// Cleans up any in-flight relight jobs for the given coordinate.
        /// Called during chunk unload. Force-completes and disposes immediately.
        /// </summary>
        public void CleanupCoord(int3 coord)
        {
            for (int i = _inFlightRelights.Count - 1; i >= 0; i--)
            {
                if (_inFlightRelights[i].Chunk.Coord.Equals(coord))
                {
                    _inFlightRelights[i].Handle.Complete();
                    _inFlightRelights[i].Chunk.LightJobInFlight = false;
                    _inFlightRelights[i].ChangedIndices.Dispose();
                    _inFlightRelights[i].BorderRemovalSeeds.Dispose();
                    _inFlightRelights[i].BorderLightOutput.Dispose();
                    _borderEntryListPool.Push(_inFlightRelights[i].OldBorderEntries);
                    _inFlightRelights.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Complete and dispose all in-flight relight jobs. Called during shutdown.
        /// </summary>
        public void Shutdown()
        {
            for (int i = 0; i < _inFlightRelights.Count; i++)
            {
                _inFlightRelights[i].Handle.Complete();
                _inFlightRelights[i].ChangedIndices.Dispose();
                _inFlightRelights[i].BorderRemovalSeeds.Dispose();
                _inFlightRelights[i].BorderLightOutput.Dispose();
            }

            _inFlightRelights.Clear();
        }

        /// <summary>
        /// After a LightRemovalJob completes, compares the chunk's old border light
        /// entries with the new post-relight border state. For any face where light
        /// decreased, creates removal seeds for the neighbor chunk (setting it to
        /// RelightPending). For any face where light increased, marks the neighbor
        /// for LightUpdateJob (NeedsLightUpdate). Also checks if THIS chunk should
        /// receive incoming light from neighbors (e.g., sunlight column restoration
        /// from the chunk above after block removal).
        /// Uses a flat byte[SizeCubed] lookup for O(1) old→new comparison instead
        /// of O(N²) nested List scans.
        /// </summary>
        private void ProcessBorderCascade(PendingRelight entry)
        {
            ManagedChunk chunk = entry.Chunk;
            List<BorderLightEntry> oldEntries = entry.OldBorderEntries;
            NativeList<NativeBorderLightEntry> newOutput = entry.BorderLightOutput;

            // Update chunk's border entries with post-relight values
            chunk.BorderLightEntries.Clear();

            for (int i = 0; i < newOutput.Length; i++)
            {
                NativeBorderLightEntry native = newOutput[i];
                chunk.BorderLightEntries.Add(new BorderLightEntry
                {
                    LocalPosition = native.LocalPosition,
                    PackedLight = native.PackedLight,
                    Face = native.Face,
                });
            }

            // Build O(1) lookup: flat voxel index → old packed light (0 = absent).
            // PackedLight is never 0 for border entries (filtered by sun > 1 || block > 1
            // in CollectBorderLightLeaks), so 0 is a safe sentinel.
            System.Array.Clear(_oldBorderLookup, 0, _oldBorderLookup.Length);

            for (int i = 0; i < oldEntries.Count; i++)
            {
                int3 pos = oldEntries[i].LocalPosition;
                int flatIdx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
                _oldBorderLookup[flatIdx] = oldEntries[i].PackedLight;
            }

            // For each face, compare old vs new border entries and cascade to neighbors
            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + _faceOffsets[f];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null ||
                    neighbor.State < ChunkState.RelightPending ||
                    !neighbor.LightData.IsCreated)
                {
                    continue;
                }

                bool hasDecreased = false;
                bool hasIncreased = false;

                // Check for decreases: old entries on this face whose light has dropped
                for (int oi = 0; oi < oldEntries.Count; oi++)
                {
                    if (oldEntries[oi].Face != f)
                    {
                        continue;
                    }

                    int3 pos = oldEntries[oi].LocalPosition;
                    int idx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
                    byte newPacked = chunk.LightData[idx];
                    byte oldPacked = oldEntries[oi].PackedLight;

                    byte oldSun = LightUtils.GetSunLight(oldPacked);
                    byte oldBlock = LightUtils.GetBlockLight(oldPacked);
                    byte newSun = LightUtils.GetSunLight(newPacked);
                    byte newBlock = LightUtils.GetBlockLight(newPacked);

                    if (newSun < oldSun || newBlock < oldBlock)
                    {
                        hasDecreased = true;
                        int3 neighborLocal = MapBorderToNeighborLocal(pos, f);
                        neighbor.PendingBorderRemovals.Add(new BorderLightEntry
                        {
                            LocalPosition = neighborLocal,
                            PackedLight = oldPacked,
                            Face = (byte)_oppositeFace[f],
                        });
                    }
                }

                // Check for increases: new entries on this face with higher light than old.
                // O(N) with flat array lookup instead of O(N²) nested scan.
                for (int ni = 0; ni < chunk.BorderLightEntries.Count; ni++)
                {
                    if (chunk.BorderLightEntries[ni].Face != f)
                    {
                        continue;
                    }

                    byte newPacked = chunk.BorderLightEntries[ni].PackedLight;
                    int3 pos = chunk.BorderLightEntries[ni].LocalPosition;
                    int flatIdx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
                    byte oldPacked = _oldBorderLookup[flatIdx];

                    if (oldPacked != 0)
                    {
                        // Old entry existed at this position — compare
                        if (LightUtils.GetSunLight(newPacked) > LightUtils.GetSunLight(oldPacked) ||
                            LightUtils.GetBlockLight(newPacked) > LightUtils.GetBlockLight(oldPacked))
                        {
                            hasIncreased = true;
                            break;
                        }
                    }
                    else
                    {
                        // New entry with no old counterpart — light appeared
                        hasIncreased = true;
                        break;
                    }
                }

                // Cascade decreases: set neighbor to RelightPending for removal BFS
                if (hasDecreased &&
                    neighbor.State >= ChunkState.Generated &&
                    !neighbor.LightJobInFlight)
                {
                    if (neighbor.State == ChunkState.Meshing)
                    {
                        // Don't force-complete the mesh job. Instead, mark the chunk so that
                        // MeshScheduler.PollCompleted transitions it to RelightPending after
                        // the mesh job finishes (similar to DeferredEdits/NeedsRemesh path).
                        neighbor.NeedsRelightAfterMesh = true;
                    }
                    else if (_cascadesThisFrame < _maxCascadesPerFrame)
                    {
                        neighbor.State = ChunkState.RelightPending;
                        _cascadesThisFrame++;
                    }
                    else
                    {
                        // Budget exceeded — the PendingBorderRemovals are already added,
                        // so FillChunksNeedingRelight will pick this up once the chunk
                        // is set to RelightPending next frame via a future cascade.
                        neighbor.State = ChunkState.RelightPending;
                    }
                }

                // Cascade increases: mark neighbor for LightUpdateJob
                if (hasIncreased)
                {
                    neighbor.NeedsLightUpdate = true;
                }
            }

            // Check if THIS chunk should receive incoming light from neighbors
            // (e.g., sunlight column restoration from the chunk above after block removal,
            // or torch light from a neighboring chunk after this chunk's border cleared)
            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + _faceOffsets[f];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null || neighbor.BorderLightEntries.Count == 0)
                {
                    continue;
                }

                int oppFace = _oppositeFace[f];

                for (int ni = 0; ni < neighbor.BorderLightEntries.Count; ni++)
                {
                    if (neighbor.BorderLightEntries[ni].Face == oppFace)
                    {
                        chunk.NeedsLightUpdate = true;

                        break;
                    }
                }

                if (chunk.NeedsLightUpdate)
                {
                    break;
                }
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

        private bool IsRelightInFlight(ManagedChunk chunk)
        {
            for (int i = 0; i < _inFlightRelights.Count; i++)
            {
                if (_inFlightRelights[i].Chunk.Coord.Equals(chunk.Coord))
                {
                    return true;
                }
            }

            return false;
        }

        private struct PendingRelight
        {
            public ManagedChunk Chunk;
            public JobHandle Handle;
            public int FrameAge;
            public NativeArray<int> ChangedIndices;
            public NativeArray<NativeBorderLightEntry> BorderRemovalSeeds;
            public NativeList<NativeBorderLightEntry> BorderLightOutput;
            public List<BorderLightEntry> OldBorderEntries;
        }
    }
}
