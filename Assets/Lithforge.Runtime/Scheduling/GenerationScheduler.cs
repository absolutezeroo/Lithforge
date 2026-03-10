using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the generation job queue: scheduling worldgen/storage-load,
    /// polling for completion, running decoration, and disposing resources.
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

                        _chunkManager.InvalidateReadyNeighbors(pending.Coord);
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

        public void ScheduleJobs()
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
                        ChunkConstants.Volume,
                        Allocator.Persistent, NativeArrayOptions.ClearMemory);

                    if (_worldStorage.LoadChunk(chunk.Coord, chunk.Data, lightData))
                    {
                        chunk.LightData = lightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;

                        _chunkManager.InvalidateReadyNeighbors(chunk.Coord);

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

        public void CleanupCoord(int3 coord)
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
        }

        public void Shutdown()
        {
            for (int i = 0; i < _pendingGenerations.Count; i++)
            {
                _pendingGenerations[i].Handle.FinalHandle.Complete();
                _pendingGenerations[i].Handle.DisposeAll();
            }

            _pendingGenerations.Clear();
        }

        private struct PendingGeneration
        {
            public int3 Coord;
            public GenerationHandle Handle;
        }
    }
}
