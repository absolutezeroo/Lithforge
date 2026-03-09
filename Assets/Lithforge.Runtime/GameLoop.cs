using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime
{
    public sealed class GameLoop : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private GenerationPipeline _generationPipeline;
        private NativeStateRegistry _nativeStateRegistry;
        private NativeAtlasLookup _nativeAtlasLookup;
        private ChunkRenderManager _chunkRenderManager;
        private DecorationStage _decorationStage;
        private WorldStorage _worldStorage;
        private long _seed;

        private readonly List<PendingGeneration> _pendingGenerations = new List<PendingGeneration>();
        private readonly List<PendingMesh> _pendingMeshes = new List<PendingMesh>();
        private readonly List<int3> _unloadedCoords = new List<int3>();

        private const int _maxGenerationsPerFrame = 4;
        private const int _maxMeshesPerFrame = 4;

        private static readonly int3[] _neighborOffsets = new int3[]
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1),
        };

        private bool _initialized;
        private bool _spawnReady;
        private Transform _playerTransform;
        private int3 _spawnChunkCoord;

        public int PendingGenerationCount
        {
            get { return _pendingGenerations.Count; }
        }

        public int PendingMeshCount
        {
            get { return _pendingMeshes.Count; }
        }

        /// <summary>
        /// True once the spawn area chunks are generated and the player has been placed.
        /// </summary>
        public bool SpawnReady
        {
            get { return _spawnReady; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            GenerationPipeline generationPipeline,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkRenderManager chunkRenderManager,
            DecorationStage decorationStage,
            WorldStorage worldStorage,
            long seed)
        {
            _chunkManager = chunkManager;
            _generationPipeline = generationPipeline;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkRenderManager = chunkRenderManager;
            _decorationStage = decorationStage;
            _worldStorage = worldStorage;
            _seed = seed;
            _spawnReady = false;
            _initialized = true;
        }

        /// <summary>
        /// Sets the player transform for safe spawn placement.
        /// Must be called after Initialize.
        /// </summary>
        public void SetPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;

            // Compute spawn chunk coord from player's initial position
            Vector3 pos = playerTransform.position;
            _spawnChunkCoord = new int3(
                (int)math.floor(pos.x / ChunkConstants.Size),
                (int)math.floor(pos.y / ChunkConstants.Size),
                (int)math.floor(pos.z / ChunkConstants.Size));
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            PollGenerationJobs();
            PollMeshJobs();

            int3 cameraChunkCoord = GetCameraChunkCoord();

            _chunkManager.UpdateLoadingQueue(cameraChunkCoord);
            _chunkManager.UnloadDistantChunks(cameraChunkCoord, _unloadedCoords);

            // Check spawn readiness before player can move
            if (!_spawnReady)
            {
                CheckSpawnReady();
            }

            for (int i = 0; i < _unloadedCoords.Count; i++)
            {
                int3 coord = _unloadedCoords[i];

                _chunkRenderManager.DestroyRenderer(coord);
                CleanupPendingJobsForCoord(coord);
            }

            ScheduleGenerationJobs();
            ScheduleMeshJobs();
        }

        private void PollGenerationJobs()
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
                            _decorationStage.Decorate(
                                pending.Coord,
                                chunk.Data,
                                pending.Handle.HeightMap,
                                pending.Handle.BiomeMap,
                                _seed);
                        }

                        // Transfer LightData ownership to chunk
                        chunk.LightData = pending.Handle.LightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;
                        pending.Handle.Dispose();

                        InvalidateReadyNeighbors(pending.Coord);
                    }
                    else
                    {
                        // Chunk was unloaded — dispose everything including LightData
                        pending.Handle.DisposeAll();
                    }

                    _pendingGenerations.RemoveAt(i);
                }
            }
        }

        private void PollMeshJobs()
        {
            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                PendingMesh pending = _pendingMeshes[i];

                if (pending.Handle.IsCompleted)
                {
                    pending.Handle.Complete();

                    _chunkRenderManager.UpdateRenderer(
                        pending.Coord,
                        pending.Data.OpaqueVertices,
                        pending.Data.OpaqueIndices,
                        pending.Data.TranslucentVertices,
                        pending.Data.TranslucentIndices);

                    pending.Data.Dispose();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        if (chunk.NeedsRemesh)
                        {
                            // Neighbor generated while we were meshing — redo with correct borders
                            chunk.NeedsRemesh = false;
                            chunk.State = ChunkState.Generated;
                        }
                        else
                        {
                            chunk.State = ChunkState.Ready;
                        }

                        chunk.ActiveJobHandle = default;
                    }

                    _pendingMeshes.RemoveAt(i);
                }
            }
        }

        private void ScheduleGenerationJobs()
        {
            int slotsAvailable = _maxGenerationsPerFrame - _pendingGenerations.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            List<ManagedChunk> chunks = _chunkManager.GetChunksToGenerate(slotsAvailable);

            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];

                // Try loading from storage first
                if (_worldStorage != null && _worldStorage.HasChunk(chunk.Coord))
                {
                    NativeArray<byte> lightData = new NativeArray<byte>(
                        Lithforge.Voxel.Chunk.ChunkConstants.Volume,
                        Allocator.Persistent, NativeArrayOptions.ClearMemory);

                    if (_worldStorage.LoadChunk(chunk.Coord, chunk.Data, lightData))
                    {
                        chunk.LightData = lightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;

                        InvalidateReadyNeighbors(chunk.Coord);

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
            }
        }

        private void ScheduleMeshJobs()
        {
            int slotsAvailable = _maxMeshesPerFrame - _pendingMeshes.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            List<ManagedChunk> chunks = _chunkManager.GetChunksToMesh(slotsAvailable);

            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];
                chunk.State = ChunkState.Meshing;

                GreedyMeshData meshData = new GreedyMeshData(Allocator.TempJob);

                // Extract neighbor border slices for cross-chunk culling
                ExtractNeighborBorders(chunk.Coord, meshData);

                GreedyMeshJob meshJob = new GreedyMeshJob
                {
                    ChunkData = chunk.Data,
                    NeighborPosX = meshData.NeighborPosX,
                    NeighborNegX = meshData.NeighborNegX,
                    NeighborPosY = meshData.NeighborPosY,
                    NeighborNegY = meshData.NeighborNegY,
                    NeighborPosZ = meshData.NeighborPosZ,
                    NeighborNegZ = meshData.NeighborNegZ,
                    StateTable = _nativeStateRegistry.States,
                    AtlasEntries = _nativeAtlasLookup.Entries,
                    LightData = chunk.LightData,
                    OpaqueVertices = meshData.OpaqueVertices,
                    OpaqueIndices = meshData.OpaqueIndices,
                    TranslucentVertices = meshData.TranslucentVertices,
                    TranslucentIndices = meshData.TranslucentIndices,
                };

                JobHandle meshHandle = meshJob.Schedule();
                chunk.ActiveJobHandle = meshHandle;

                _pendingMeshes.Add(new PendingMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = meshData,
                });
            }
        }

        private void ExtractNeighborBorders(int3 coord, GreedyMeshData meshData)
        {
            // +X neighbor: chunk at (x+1), extract its -X face (face 1)
            ExtractBorderFromNeighbor(coord + new int3(1, 0, 0), 1, meshData.NeighborPosX);
            // -X neighbor: chunk at (x-1), extract its +X face (face 0)
            ExtractBorderFromNeighbor(coord + new int3(-1, 0, 0), 0, meshData.NeighborNegX);
            // +Y neighbor: chunk at (y+1), extract its -Y face (face 3)
            ExtractBorderFromNeighbor(coord + new int3(0, 1, 0), 3, meshData.NeighborPosY);
            // -Y neighbor: chunk at (y-1), extract its +Y face (face 2)
            ExtractBorderFromNeighbor(coord + new int3(0, -1, 0), 2, meshData.NeighborNegY);
            // +Z neighbor: chunk at (z+1), extract its -Z face (face 5)
            ExtractBorderFromNeighbor(coord + new int3(0, 0, 1), 5, meshData.NeighborPosZ);
            // -Z neighbor: chunk at (z-1), extract its +Z face (face 4)
            ExtractBorderFromNeighbor(coord + new int3(0, 0, -1), 4, meshData.NeighborNegZ);
        }

        private void ExtractBorderFromNeighbor(int3 neighborCoord, int faceDirection, NativeArray<StateId> output)
        {
            ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

            if (neighbor != null && neighbor.State >= ChunkState.Generated && neighbor.Data.IsCreated)
            {
                ChunkBorderExtractor.ExtractBorder(neighbor.Data, faceDirection, output);
            }
            // If neighbor doesn't exist, output stays zero-initialized (air)
        }

        private void InvalidateReadyNeighbors(int3 coord)
        {
            for (int i = 0; i < _neighborOffsets.Length; i++)
            {
                int3 neighborCoord = coord + _neighborOffsets[i];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null)
                {
                    continue;
                }

                if (neighbor.State == ChunkState.Ready)
                {
                    neighbor.State = ChunkState.Generated;
                }
                else if (neighbor.State == ChunkState.Meshing)
                {
                    // Job is active — flag for re-mesh after it completes
                    neighbor.NeedsRemesh = true;
                }
            }
        }

        private void CleanupPendingJobsForCoord(int3 coord)
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                if (_pendingGenerations[i].Coord.Equals(coord))
                {
                    _pendingGenerations[i].Handle.FinalHandle.Complete();
                    _pendingGenerations[i].Handle.DisposeAll();
                    _pendingGenerations.RemoveAt(i);
                }
            }

            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                if (_pendingMeshes[i].Coord.Equals(coord))
                {
                    _pendingMeshes[i].Handle.Complete();
                    _pendingMeshes[i].Data.Dispose();
                    _pendingMeshes.RemoveAt(i);
                }
            }
        }

        private int3 GetCameraChunkCoord()
        {
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

            return new int3(
                (int)math.floor(camPos.x / ChunkConstants.Size),
                (int)math.floor(camPos.y / ChunkConstants.Size),
                (int)math.floor(camPos.z / ChunkConstants.Size));
        }

        public void Shutdown()
        {
            for (int i = 0; i < _pendingGenerations.Count; i++)
            {
                _pendingGenerations[i].Handle.FinalHandle.Complete();
                _pendingGenerations[i].Handle.DisposeAll();
            }

            _pendingGenerations.Clear();

            for (int i = 0; i < _pendingMeshes.Count; i++)
            {
                _pendingMeshes[i].Handle.Complete();
                _pendingMeshes[i].Data.Dispose();
            }

            _pendingMeshes.Clear();
        }

        /// <summary>
        /// Checks if all chunks in a 1-chunk radius around spawn are generated.
        /// Once ready, places the player on the highest solid block and enables movement.
        /// </summary>
        private void CheckSpawnReady()
        {
            // Check a 3x3x3 column of chunks around spawn (XZ radius 1, Y from -1 to +1)
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        int3 coord = _spawnChunkCoord + new int3(x, y, z);
                        ManagedChunk chunk = _chunkManager.GetChunk(coord);

                        if (chunk == null || chunk.State < ChunkState.Generated)
                        {
                            return;
                        }
                    }
                }
            }

            // All spawn chunks are ready — find safe spawn position
            if (_playerTransform != null)
            {
                int spawnX = _spawnChunkCoord.x * ChunkConstants.Size + ChunkConstants.Size / 2;
                int spawnZ = _spawnChunkCoord.z * ChunkConstants.Size + ChunkConstants.Size / 2;
                int safeY = FindSafeSpawnY(spawnX, spawnZ);

                _playerTransform.position = new Vector3(spawnX + 0.5f, safeY, spawnZ + 0.5f);
                UnityEngine.Debug.Log($"[Lithforge] Spawn ready at ({spawnX}, {safeY}, {spawnZ})");
            }

            _spawnReady = true;
        }

        /// <summary>
        /// Scans downward from the sky to find the highest air block above a solid block.
        /// Returns the Y coordinate where the player's feet should be placed.
        /// </summary>
        private int FindSafeSpawnY(int worldX, int worldZ)
        {
            // Scan from top of loaded range down to find ground
            int maxY = (_spawnChunkCoord.y + 2) * ChunkConstants.Size - 1;
            int minY = (_spawnChunkCoord.y - 1) * ChunkConstants.Size;

            for (int y = maxY; y >= minY; y--)
            {
                int3 blockCoord = new int3(worldX, y, worldZ);
                StateId stateId = _chunkManager.GetBlock(blockCoord);
                BlockStateCompact state = _nativeStateRegistry.States[stateId.Value];

                if (state.CollisionShape != 0)
                {
                    // Found solid block — player stands on top of it
                    return y + 1;
                }
            }

            // No solid block found — use sea level as fallback
            return 65;
        }

        private struct PendingGeneration
        {
            public int3 Coord;
            public GenerationHandle Handle;
        }

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
        }
    }
}
