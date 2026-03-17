using System;
using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Liquid;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Main-thread orchestrator for the liquid simulation system.
    /// Collects candidate chunks, partitions into even/odd parity for checkerboard scheduling,
    /// copies ghost slabs from neighbor LiquidData, schedules <see cref="LiquidSimJob"/> Burst jobs,
    /// polls completion, and applies results via ChunkManager.SetBlock().
    ///
    /// Integration:
    /// - <see cref="LiquidTickAdapter"/> calls <see cref="OnSimTick"/> every 8 game ticks.
    /// - <see cref="PollCompleted"/> is called every frame from GameLoop.Update().
    /// - Chunk unload handled via <see cref="OnChunkUnloaded"/>.
    /// </summary>
    public sealed class LiquidScheduler : IDisposable
    {
        private readonly ChunkManager _chunkManager;
        private readonly LiquidPool _liquidPool;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly LiquidJobConfig _waterConfig;

        private readonly List<PendingLiquidJob> _inFlightJobs;
        private readonly HashSet<int3> _inFlightCoords;
        private readonly List<ManagedChunk> _candidateCache;
        private readonly List<int3> _dirtiedChunksCache;
        private readonly List<int> _fullScanCache;
        private readonly Dictionary<int3, JobHandle> _evenHandles;
        private readonly HashSet<int3> _forceCompleteCache;

        private MeshScheduler _meshScheduler;
        private bool _applyingLiquidResults;
        private bool _disposed;

        public LiquidScheduler(
            ChunkManager chunkManager,
            LiquidPool liquidPool,
            NativeStateRegistry nativeStateRegistry,
            LiquidJobConfig waterConfig)
        {
            _chunkManager = chunkManager;
            _liquidPool = liquidPool;
            _nativeStateRegistry = nativeStateRegistry;
            _waterConfig = waterConfig;

            _inFlightJobs = new List<PendingLiquidJob>(64);
            _inFlightCoords = new HashSet<int3>();
            _candidateCache = new List<ManagedChunk>(128);
            _dirtiedChunksCache = new List<int3>(16);
            _fullScanCache = new List<int>(256);
            _evenHandles = new Dictionary<int3, JobHandle>(64);
            _forceCompleteCache = new HashSet<int3>();
        }

        /// <summary>
        /// Wires the MeshScheduler so liquid result application can force-complete
        /// in-flight mesh jobs that read target chunk data as neighbor borders.
        /// Called by GameLoop.SetLiquidScheduler.
        /// </summary>
        public void SetMeshScheduler(MeshScheduler meshScheduler)
        {
            _meshScheduler = meshScheduler;
        }

        /// <summary>
        /// Initializes liquid data for a newly generated chunk that contains water.
        /// Scans BlockData for fluid StateIds and sets corresponding source cells.
        /// Called by GenerationScheduler after decoration completes.
        /// </summary>
        public void InitChunkLiquid(ManagedChunk chunk)
        {
            if (chunk.LiquidData.IsCreated)
            {
                return;
            }

            // Scan for fluid blocks
            bool hasFluid = false;
            NativeArray<StateId> blockData = chunk.Data;

            for (int i = 0; i < blockData.Length; i++)
            {
                StateId stateId = blockData[i];
                BlockStateCompact compact = _nativeStateRegistry.States[stateId.Value];

                if (compact.IsFluid)
                {
                    hasFluid = true;
                    break;
                }
            }

            if (!hasFluid)
            {
                return;
            }

            NativeArray<byte> liquidData = _liquidPool.Checkout();

            for (int i = 0; i < blockData.Length; i++)
            {
                StateId stateId = blockData[i];
                BlockStateCompact compact = _nativeStateRegistry.States[stateId.Value];

                if (compact.IsFluid)
                {
                    liquidData[i] = LiquidCell.MakeSource();
                }
            }

            chunk.LiquidData = liquidData;
            chunk.LiquidActiveSet = null; // null = needs full active scan first tick
        }

        /// <summary>
        /// Called every 8 game ticks by <see cref="LiquidTickAdapter"/>.
        /// Schedules liquid sim jobs for eligible chunks using checkerboard partitioning.
        /// </summary>
        public void OnSimTick()
        {
            if (_disposed)
            {
                return;
            }

            int3 cameraChunkCoord = _chunkManager.GetCameraChunkCoord();

            // Collect candidate chunks
            _candidateCache.Clear();
            _chunkManager.FillChunksWithLiquid(_candidateCache, cameraChunkCoord, LiquidConstants.SimRadius);

            if (_candidateCache.Count == 0)
            {
                return;
            }

            _evenHandles.Clear();

            int scheduled = 0;

            // Phase 1: Schedule even parity chunks
            for (int i = 0; i < _candidateCache.Count && scheduled < LiquidConstants.MaxJobsPerTick; i++)
            {
                ManagedChunk chunk = _candidateCache[i];
                int3 coord = chunk.Coord;

                if (_inFlightCoords.Contains(coord))
                {
                    continue;
                }

                int parity = (coord.x + coord.y + coord.z) & 1;

                if (parity != 0)
                {
                    continue;
                }

                if (chunk.State < ChunkState.Generated || chunk.State == ChunkState.Meshing)
                {
                    continue;
                }

                PendingLiquidJob pending = ScheduleJob(chunk, default(JobHandle), 0);
                chunk.LiquidJobHandle = pending.Handle;
                _inFlightJobs.Add(pending);
                _inFlightCoords.Add(coord);
                _evenHandles[coord] = pending.Handle;
                scheduled++;
            }

            JobHandle.ScheduleBatchedJobs();

            // Phase 2: Schedule odd parity chunks with dependency on even neighbors
            for (int i = 0; i < _candidateCache.Count && scheduled < LiquidConstants.MaxJobsPerTick; i++)
            {
                ManagedChunk chunk = _candidateCache[i];
                int3 coord = chunk.Coord;

                if (_inFlightCoords.Contains(coord))
                {
                    continue;
                }

                int parity = (coord.x + coord.y + coord.z) & 1;

                if (parity != 1)
                {
                    continue;
                }

                if (chunk.State < ChunkState.Generated || chunk.State == ChunkState.Meshing)
                {
                    continue;
                }

                // Build dependency on even-parity neighbors
                JobHandle dependency = default(JobHandle);
                int depCount = 0;

                for (int face = 0; face < 6; face++)
                {
                    int3 neighborCoord = coord + ChunkManager.FaceOffsets[face];

                    if (_evenHandles.TryGetValue(neighborCoord, out JobHandle evenHandle))
                    {
                        if (depCount == 0)
                        {
                            dependency = evenHandle;
                        }
                        else
                        {
                            dependency = JobHandle.CombineDependencies(dependency, evenHandle);
                        }

                        depCount++;
                    }
                }

                PendingLiquidJob pending = ScheduleJob(chunk, dependency, 1);
                chunk.LiquidJobHandle = pending.Handle;
                _inFlightJobs.Add(pending);
                _inFlightCoords.Add(coord);
                scheduled++;
            }

            if (scheduled > 0)
            {
                JobHandle.ScheduleBatchedJobs();
            }
        }

        private PendingLiquidJob ScheduleJob(ManagedChunk chunk, JobHandle dependency, byte parity)
        {
            NativeArray<int> inputActiveSet = BuildInputActiveSet(chunk);

            // Allocate output containers
            NativeList<LiquidChunkEdit> outputEdits = new NativeList<LiquidChunkEdit>(
                64, Allocator.TempJob);
            NativeList<int> outputActiveSet = new NativeList<int>(
                inputActiveSet.Length, Allocator.TempJob);
            NativeArray<byte> bfsVisited = new NativeArray<byte>(
                128, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            // Copy ghost slabs from neighbors
            int slabSize = ChunkConstants.SizeSquared;
            NativeArray<byte> ghostPosX = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostNegX = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostPosY = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostNegY = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostPosZ = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostNegZ = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            CopyGhostSlab(chunk, 0, ghostPosX);
            CopyGhostSlab(chunk, 1, ghostNegX);
            CopyGhostSlab(chunk, 2, ghostPosY);
            CopyGhostSlab(chunk, 3, ghostNegY);
            CopyGhostSlab(chunk, 4, ghostPosZ);
            CopyGhostSlab(chunk, 5, ghostNegZ);

            // Block-solidity ghost slabs for cross-boundary BFS flow-direction search
            NativeArray<byte> ghostBlockSolidPosX = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostBlockSolidNegX = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostBlockSolidPosZ = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> ghostBlockSolidNegZ = new NativeArray<byte>(slabSize, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            CopyBlockSolidGhostSlab(chunk, 0, ghostBlockSolidPosX);
            CopyBlockSolidGhostSlab(chunk, 1, ghostBlockSolidNegX);
            CopyBlockSolidGhostSlab(chunk, 4, ghostBlockSolidPosZ);
            CopyBlockSolidGhostSlab(chunk, 5, ghostBlockSolidNegZ);

            LiquidSimJob job = new LiquidSimJob
            {
                LiquidData = chunk.LiquidData,
                BlockData = chunk.Data,
                StateTable = _nativeStateRegistry.States,
                InputActiveSet = inputActiveSet,
                GhostPosX = ghostPosX,
                GhostNegX = ghostNegX,
                GhostPosY = ghostPosY,
                GhostNegY = ghostNegY,
                GhostPosZ = ghostPosZ,
                GhostNegZ = ghostNegZ,
                GhostBlockSolidPosX = ghostBlockSolidPosX,
                GhostBlockSolidNegX = ghostBlockSolidNegX,
                GhostBlockSolidPosZ = ghostBlockSolidPosZ,
                GhostBlockSolidNegZ = ghostBlockSolidNegZ,
                BfsVisited = bfsVisited,
                Config = _waterConfig,
                OutputEdits = outputEdits,
                OutputActiveSet = outputActiveSet,
            };

            JobHandle handle = job.Schedule(dependency);

            return new PendingLiquidJob
            {
                ChunkCoord = chunk.Coord,
                Handle = handle,
                OutputEdits = outputEdits,
                OutputActiveSet = outputActiveSet,
                InputActiveSet = inputActiveSet,
                BfsVisited = bfsVisited,
                GhostPosX = ghostPosX,
                GhostNegX = ghostNegX,
                GhostPosY = ghostPosY,
                GhostNegY = ghostNegY,
                GhostPosZ = ghostPosZ,
                GhostNegZ = ghostNegZ,
                GhostBlockSolidPosX = ghostBlockSolidPosX,
                GhostBlockSolidNegX = ghostBlockSolidNegX,
                GhostBlockSolidPosZ = ghostBlockSolidPosZ,
                GhostBlockSolidNegZ = ghostBlockSolidNegZ,
                FrameAge = 0,
                Parity = parity,
            };
        }

        private NativeArray<int> BuildInputActiveSet(ManagedChunk chunk)
        {
            if (chunk.LiquidActiveSet == null)
            {
                // null = full scan: add all non-empty liquid voxels
                _fullScanCache.Clear();

                for (int i = 0; i < chunk.LiquidData.Length; i++)
                {
                    byte cell = chunk.LiquidData[i];

                    if (LiquidCell.HasLiquid(cell) && !LiquidCell.IsSettled(cell))
                    {
                        _fullScanCache.Add(i);
                    }
                }

                NativeArray<int> result = new NativeArray<int>(
                    _fullScanCache.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < _fullScanCache.Count; i++)
                {
                    result[i] = _fullScanCache[i];
                }

                return result;
            }
            else
            {
                NativeArray<int> result = new NativeArray<int>(
                    chunk.LiquidActiveSet.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < chunk.LiquidActiveSet.Count; i++)
                {
                    result[i] = chunk.LiquidActiveSet[i];
                }

                return result;
            }
        }

        private void CopyGhostSlab(ManagedChunk chunk, int face, NativeArray<byte> output)
        {
            ManagedChunk neighbor = chunk.Neighbors[face];

            if (neighbor == null || !neighbor.LiquidData.IsCreated)
            {
                return; // output stays zeroed (empty liquid)
            }

            // Skip if neighbor has an in-flight liquid job writing to its LiquidData.
            // The ghost slab will be zeroed (slightly stale), self-correcting next tick.
            if (_inFlightCoords.Contains(neighbor.Coord))
            {
                return;
            }

            NativeArray<byte> neighborLiquid = neighbor.LiquidData;
            int size = ChunkConstants.Size;
            int lastIdx = size - 1;

            switch (face)
            {
                case 0: // +X: extract neighbor's x=0 face, layout [y * size + z]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(0, v, u);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;

                case 1: // -X: extract neighbor's x=31 face
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(lastIdx, v, u);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;

                case 2: // +Y: extract neighbor's y=0 face, layout [z * size + x]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, 0, v);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;

                case 3: // -Y: extract neighbor's y=31 face
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, lastIdx, v);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;

                case 4: // +Z: extract neighbor's z=0 face, layout [y * size + x]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, v, 0);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;

                case 5: // -Z: extract neighbor's z=31 face
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, v, lastIdx);
                            output[v * size + u] = neighborLiquid[srcIndex];
                        }
                    }

                    break;
            }
        }

        private void CopyBlockSolidGhostSlab(ManagedChunk chunk, int face, NativeArray<byte> output)
        {
            ManagedChunk neighbor = chunk.Neighbors[face];

            if (neighbor == null || !neighbor.Data.IsCreated)
            {
                return; // output stays zeroed (non-solid)
            }

            if (_inFlightCoords.Contains(neighbor.Coord))
            {
                return;
            }

            // Skip neighbors with in-flight jobs that hold BlockData safety handles.
            // Generation/decoration jobs write BlockData; mesh/light jobs hold it [ReadOnly].
            // Either case blocks main-thread reads via Unity's safety system.
            if (neighbor.State < ChunkState.Generated || neighbor.State == ChunkState.Meshing)
            {
                return;
            }

            NativeArray<StateId> neighborBlocks = neighbor.Data;
            NativeArray<BlockStateCompact> stateTable = _nativeStateRegistry.States;
            int size = ChunkConstants.Size;
            int lastIdx = size - 1;

            switch (face)
            {
                case 0: // +X: neighbor's x=0 face, layout [y * size + z]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(0, v, u);
                            StateId sid = neighborBlocks[srcIndex];
                            BlockStateCompact c = stateTable[sid.Value];
                            output[v * size + u] = (byte)((c.CollisionShape != 0 && !c.IsFluid) ? 1 : 0);
                        }
                    }

                    break;

                case 1: // -X: neighbor's x=31 face
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(lastIdx, v, u);
                            StateId sid = neighborBlocks[srcIndex];
                            BlockStateCompact c = stateTable[sid.Value];
                            output[v * size + u] = (byte)((c.CollisionShape != 0 && !c.IsFluid) ? 1 : 0);
                        }
                    }

                    break;

                case 4: // +Z: neighbor's z=0 face, layout [y * size + x]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, v, 0);
                            StateId sid = neighborBlocks[srcIndex];
                            BlockStateCompact c = stateTable[sid.Value];
                            output[v * size + u] = (byte)((c.CollisionShape != 0 && !c.IsFluid) ? 1 : 0);
                        }
                    }

                    break;

                case 5: // -Z: neighbor's z=31 face, layout [y * size + x]
                    for (int v = 0; v < size; v++)
                    {
                        for (int u = 0; u < size; u++)
                        {
                            int srcIndex = ChunkData.GetIndex(u, v, lastIdx);
                            StateId sid = neighborBlocks[srcIndex];
                            BlockStateCompact c = stateTable[sid.Value];
                            output[v * size + u] = (byte)((c.CollisionShape != 0 && !c.IsFluid) ? 1 : 0);
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Polls and completes in-flight liquid jobs. Called every frame from GameLoop.
        /// </summary>
        public void PollCompleted()
        {
            if (_disposed)
            {
                return;
            }

            int completions = 0;

            for (int i = _inFlightJobs.Count - 1; i >= 0; i--)
            {
                PendingLiquidJob pending = _inFlightJobs[i];
                pending.FrameAge++;
                _inFlightJobs[i] = pending;

                bool forceComplete = pending.FrameAge >= LiquidConstants.MaxJobFrameAge;

                if (!pending.Handle.IsCompleted && !forceComplete)
                {
                    continue;
                }

                if (completions >= LiquidConstants.MaxCompletionsPerFrame && !forceComplete)
                {
                    continue;
                }

                pending.Handle.Complete();

                // Clear LiquidJobHandle before applying results so MeshScheduler
                // doesn't try to depend on a completed handle for this chunk.
                ManagedChunk completedChunk = _chunkManager.TryGetChunk(pending.ChunkCoord);

                if (completedChunk != null)
                {
                    completedChunk.LiquidJobHandle = default;
                }

                ApplyJobResults(pending);
                pending.DisposeContainers();

                _inFlightCoords.Remove(pending.ChunkCoord);
                _inFlightJobs[i] = _inFlightJobs[_inFlightJobs.Count - 1];
                _inFlightJobs.RemoveAt(_inFlightJobs.Count - 1);

                completions++;
            }
        }

        private void ApplyJobResults(PendingLiquidJob pending)
        {
            // Update active set on the chunk
            ManagedChunk chunk = _chunkManager.TryGetChunk(pending.ChunkCoord);

            if (chunk == null)
            {
                return; // chunk was unloaded
            }

            // Rebuild active set from output
            if (chunk.LiquidActiveSet == null)
            {
                chunk.LiquidActiveSet = new List<int>(pending.OutputActiveSet.Length);
            }
            else
            {
                chunk.LiquidActiveSet.Clear();
            }

            for (int i = 0; i < pending.OutputActiveSet.Length; i++)
            {
                chunk.LiquidActiveSet.Add(pending.OutputActiveSet[i]);
            }

            // Force-complete any in-flight jobs that hold safety locks on data
            // we're about to write via SetBlock or direct LiquidData write:
            // 1. Mesh border extraction jobs reading target chunks as neighbors
            // 2. LightRemovalJobs reading target chunk's ChunkData
            // 3. LiquidSimJobs writing to target (neighbor) chunk's LiquidData
            // Collect unique target chunk coords first to batch force-completion.
            if (pending.OutputEdits.Length > 0)
            {
                _forceCompleteCache.Clear();
                _forceCompleteCache.Add(pending.ChunkCoord);

                for (int i = 0; i < pending.OutputEdits.Length; i++)
                {
                    LiquidChunkEdit edit = pending.OutputEdits[i];
                    int3 targetCoord = pending.ChunkCoord + edit.ChunkOffset;
                    _forceCompleteCache.Add(targetCoord);
                }

                foreach (int3 coord in _forceCompleteCache)
                {
                    // Mesh border extraction: neighbor chunks' ExtractAllBordersJob
                    if (_meshScheduler != null)
                    {
                        _meshScheduler.ForceCompleteNeighborDeps(coord);
                    }

                    ManagedChunk target = _chunkManager.TryGetChunk(coord);

                    if (target == null)
                    {
                        continue;
                    }

                    // Relight: LightRemovalJob reads ChunkData as [ReadOnly]
                    if (target.LightJobInFlight)
                    {
                        target.ActiveJobHandle.Complete();
                    }

                    // Liquid: neighbor's in-flight LiquidSimJob writes to LiquidData.
                    // Complete it so we can safely write cross-boundary edits.
                    // Skip the current chunk (already completed above in PollCompleted).
                    if (_inFlightCoords.Contains(coord) &&
                        !coord.Equals(pending.ChunkCoord))
                    {
                        target.LiquidJobHandle.Complete();
                    }
                }
            }

            // Apply edits to ChunkManager (triggers relight + remesh)
            _applyingLiquidResults = true;
            _dirtiedChunksCache.Clear();

            for (int i = 0; i < pending.OutputEdits.Length; i++)
            {
                LiquidChunkEdit edit = pending.OutputEdits[i];

                int3 targetChunkCoord = pending.ChunkCoord + edit.ChunkOffset;

                if (edit.ChunkOffset.x == 0 && edit.ChunkOffset.y == 0 && edit.ChunkOffset.z == 0)
                {
                    // Same chunk: already written to LiquidData by the job.
                    // Just update the block StateId.
                    int y = edit.FlatIndex / ChunkConstants.SizeSquared;
                    int remainder = edit.FlatIndex - (y * ChunkConstants.SizeSquared);
                    int z = remainder / ChunkConstants.Size;
                    int x = remainder - (z * ChunkConstants.Size);

                    int3 worldCoord = chunk.Coord * ChunkConstants.Size + new int3(x, y, z);
                    _chunkManager.SetBlock(worldCoord, edit.NewStateId, _dirtiedChunksCache);
                }
                else
                {
                    // Cross-boundary edit: update both LiquidData and BlockData on neighbor
                    ManagedChunk targetChunk = _chunkManager.TryGetChunk(targetChunkCoord);

                    if (targetChunk != null)
                    {
                        if (!targetChunk.LiquidData.IsCreated)
                        {
                            targetChunk.LiquidData = _liquidPool.Checkout();
                            targetChunk.LiquidActiveSet = null;
                        }

                        NativeArray<byte> targetLiquid = targetChunk.LiquidData;
                        targetLiquid[edit.FlatIndex] = edit.NewLiquidCell;
                        targetChunk.LiquidActiveSet = null; // force full scan next tick

                        int y = edit.FlatIndex / ChunkConstants.SizeSquared;
                        int remainder = edit.FlatIndex - (y * ChunkConstants.SizeSquared);
                        int z = remainder / ChunkConstants.Size;
                        int x = remainder - (z * ChunkConstants.Size);

                        int3 worldCoord = targetChunkCoord * ChunkConstants.Size + new int3(x, y, z);
                        _chunkManager.SetBlock(worldCoord, edit.NewStateId, _dirtiedChunksCache);
                    }
                }
            }

            _applyingLiquidResults = false;
        }

        /// <summary>
        /// Called when a block is placed or broken by the player.
        /// Updates LiquidData if the target voxel contained liquid, and wakes
        /// settled liquid neighbors so flow can re-evaluate.
        /// </summary>
        public void OnBlockChanged(int3 worldCoord, StateId newStateId)
        {
            if (_disposed)
            {
                return;
            }

            if (_applyingLiquidResults)
            {
                return;
            }

            int3 chunkCoord = ChunkManager.WorldToChunk(worldCoord);
            ManagedChunk chunk = _chunkManager.TryGetChunk(chunkCoord);

            if (chunk == null)
            {
                return;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int flatIndex = ChunkData.GetIndex(localX, localY, localZ);

            // If a liquid job is in-flight for this chunk, skip LiquidData write.
            // The null active set will force a full rescan after the job completes.
            if (!_inFlightCoords.Contains(chunkCoord))
            {
                bool newIsFluid = _nativeStateRegistry.States.IsCreated &&
                    newStateId.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[newStateId.Value].IsFluid;

                if (chunk.LiquidData.IsCreated)
                {
                    if (!newIsFluid)
                    {
                        // Block placed over water, or water broken: clear the liquid cell
                        NativeArray<byte> liquidData = chunk.LiquidData;
                        liquidData[flatIndex] = LiquidCell.Empty;
                    }
                }

                if (newIsFluid)
                {
                    // Fluid block placed (e.g., water bucket): set liquid cell to source
                    if (!chunk.LiquidData.IsCreated)
                    {
                        chunk.LiquidData = _liquidPool.Checkout();
                        chunk.LiquidActiveSet = null;
                    }

                    NativeArray<byte> liquidData = chunk.LiquidData;
                    liquidData[flatIndex] = LiquidCell.MakeSource();
                }
            }

            WakeChunkAndNeighbors(chunkCoord, localX, localY, localZ, chunk);
        }

        private void WakeChunkAndNeighbors(int3 chunkCoord, int lx, int ly, int lz, ManagedChunk chunk)
        {
            chunk.LiquidActiveSet = null;

            ClearSettledAt(chunk, lx + 1, ly, lz);
            ClearSettledAt(chunk, lx - 1, ly, lz);
            ClearSettledAt(chunk, lx, ly + 1, lz);
            ClearSettledAt(chunk, lx, ly - 1, lz);
            ClearSettledAt(chunk, lx, ly, lz + 1);
            ClearSettledAt(chunk, lx, ly, lz - 1);

            int size = ChunkConstants.Size;

            if (lx == 0)
            {
                WakeNeighborChunk(chunkCoord + new int3(-1, 0, 0));
            }

            if (lx == size - 1)
            {
                WakeNeighborChunk(chunkCoord + new int3(1, 0, 0));
            }

            if (ly == 0)
            {
                WakeNeighborChunk(chunkCoord + new int3(0, -1, 0));
            }

            if (ly == size - 1)
            {
                WakeNeighborChunk(chunkCoord + new int3(0, 1, 0));
            }

            if (lz == 0)
            {
                WakeNeighborChunk(chunkCoord + new int3(0, 0, -1));
            }

            if (lz == size - 1)
            {
                WakeNeighborChunk(chunkCoord + new int3(0, 0, 1));
            }
        }

        private void ClearSettledAt(ManagedChunk chunk, int lx, int ly, int lz)
        {
            int size = ChunkConstants.Size;

            if (lx < 0 || lx >= size || ly < 0 || ly >= size || lz < 0 || lz >= size)
            {
                return;
            }

            if (!chunk.LiquidData.IsCreated)
            {
                return;
            }

            NativeArray<byte> liquidData = chunk.LiquidData;
            int idx = ChunkData.GetIndex(lx, ly, lz);
            byte cell = liquidData[idx];

            if (LiquidCell.IsSettled(cell))
            {
                liquidData[idx] = LiquidCell.ClearSettled(cell);
            }
        }

        private void WakeNeighborChunk(int3 neighborCoord)
        {
            ManagedChunk neighbor = _chunkManager.TryGetChunk(neighborCoord);

            if (neighbor != null && neighbor.LiquidData.IsCreated)
            {
                neighbor.LiquidActiveSet = null;
            }
        }

        /// <summary>
        /// Called when a chunk is unloaded. Force-completes any in-flight job for that
        /// coord and returns liquid data to the pool.
        /// </summary>
        public void OnChunkUnloaded(int3 coord)
        {
            // Force-complete in-flight job for this coord
            for (int i = _inFlightJobs.Count - 1; i >= 0; i--)
            {
                PendingLiquidJob pending = _inFlightJobs[i];

                if (pending.ChunkCoord.Equals(coord))
                {
                    pending.Handle.Complete();
                    pending.DisposeContainers();
                    _inFlightCoords.Remove(coord);
                    _inFlightJobs[i] = _inFlightJobs[_inFlightJobs.Count - 1];
                    _inFlightJobs.RemoveAt(_inFlightJobs.Count - 1);
                    break;
                }
            }

            // Return liquid data to pool
            ManagedChunk chunk = _chunkManager.TryGetChunk(coord);

            if (chunk != null && chunk.LiquidData.IsCreated)
            {
                _liquidPool.Return(chunk.LiquidData);
                chunk.LiquidData = default;
                chunk.LiquidActiveSet = null;
            }
        }

        /// <summary>
        /// Shuts down the scheduler: force-completes all in-flight jobs.
        /// </summary>
        public void Shutdown()
        {
            for (int i = 0; i < _inFlightJobs.Count; i++)
            {
                PendingLiquidJob pending = _inFlightJobs[i];
                pending.Handle.Complete();
                pending.DisposeContainers();
            }

            _inFlightJobs.Clear();
            _inFlightCoords.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Shutdown();
            _liquidPool.Dispose();
        }
    }
}
