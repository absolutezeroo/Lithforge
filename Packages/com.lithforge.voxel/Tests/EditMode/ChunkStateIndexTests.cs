using System.Collections.Generic;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using NUnit.Framework;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Tests
{
    /// <summary>Tests for <see cref="ChunkStateIndex"/> secondary index maintenance and mesh priority selection.</summary>
    [TestFixture]
    public sealed class ChunkStateIndexTests
    {
        /// <summary>Pool for allocating chunk NativeArrays.</summary>
        private ChunkPool _pool;

        /// <summary>The state index under test.</summary>
        private ChunkStateIndex _stateIndex;

        /// <summary>Reusable result list for Fill* queries.</summary>
        private List<ManagedChunk> _result;

        /// <summary>Tracks all created chunks for cleanup.</summary>
        private List<ManagedChunk> _createdChunks;

        /// <summary>Allocates pool, state index, and scratch lists.</summary>
        [SetUp]
        public void SetUp()
        {
            _pool = new ChunkPool(32);
            _stateIndex = new ChunkStateIndex();
            _result = new List<ManagedChunk>();
            _createdChunks = new List<ManagedChunk>();
        }

        /// <summary>Returns all checked-out arrays and disposes the pool.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdChunks.Count; i++)
            {
                if (_createdChunks[i].Data.IsCreated)
                {
                    _pool.Return(_createdChunks[i].Data);
                    _createdChunks[i].Data = default;
                }
            }

            _pool.Dispose();
        }

        /// <summary>SetChunkState to Generated adds chunk to generated set; Meshing removes it.</summary>
        [Test]
        public void SetChunkState_TracksGeneratedChunks()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            _stateIndex.FillGeneratedChunks(_result);

            Assert.AreEqual(1, _result.Count);
            Assert.AreSame(chunk, _result[0]);

            _stateIndex.SetChunkState(chunk, ChunkState.Meshing);
            _stateIndex.FillGeneratedChunks(_result);

            Assert.AreEqual(0, _result.Count);
        }

        /// <summary>SetChunkState to Ready adds chunk to ready set; back to Generated removes it.</summary>
        [Test]
        public void SetChunkState_TracksReadyChunks()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            _stateIndex.SetChunkState(chunk, ChunkState.Ready);
            _stateIndex.FillReadyChunks(_result);

            Assert.AreEqual(1, _result.Count);
            Assert.AreSame(chunk, _result[0]);

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            _stateIndex.FillReadyChunks(_result);

            Assert.AreEqual(0, _result.Count);
        }

        /// <summary>SetChunkState to RelightPending adds chunk to relight set; Generated removes it.</summary>
        [Test]
        public void SetChunkState_TracksRelightPendingChunks()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);

            _stateIndex.SetChunkState(chunk, ChunkState.RelightPending);
            _stateIndex.FillChunksNeedingRelight(_result);

            Assert.AreEqual(1, _result.Count);
            Assert.AreSame(chunk, _result[0]);

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            _stateIndex.FillChunksNeedingRelight(_result);

            Assert.AreEqual(0, _result.Count);
        }

        /// <summary>Generated chunk with LightJobInFlight is excluded from FillGeneratedChunks.</summary>
        [Test]
        public void NotifyLightJobChanged_ExcludesFromGenerated()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            chunk.LightJobInFlight = true;
            _stateIndex.NotifyLightJobChanged(chunk);

            _stateIndex.FillGeneratedChunks(_result);

            Assert.AreEqual(0, _result.Count, "Chunk with LightJobInFlight should be excluded");

            chunk.LightJobInFlight = false;
            _stateIndex.NotifyLightJobChanged(chunk);

            _stateIndex.FillGeneratedChunks(_result);

            Assert.AreEqual(1, _result.Count, "Chunk without LightJobInFlight should be included");
        }

        /// <summary>FillChunksToMesh returns top-K closest chunks by Manhattan distance.</summary>
        [Test]
        public void FillChunksToMesh_ReturnsTopK()
        {
            List<int3> playerCoords = new() { int3.zero };

            // Create 20 chunks at increasing distances
            for (int i = 0; i < 20; i++)
            {
                ManagedChunk chunk = CreateChunk(new int3(i, 0, 0));
                _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            }

            _stateIndex.FillChunksToMesh(_result, 5, int3.zero, new float3(0, 0, 1), playerCoords);

            Assert.AreEqual(5, _result.Count);

            // All 5 results should be within Manhattan distance 4 of origin
            for (int i = 0; i < _result.Count; i++)
            {
                int3 d = _result[i].Coord;
                int dist = math.abs(d.x) + math.abs(d.y) + math.abs(d.z);
                Assert.LessOrEqual(dist, 4, $"Chunk at {_result[i].Coord} has distance {dist}, expected <= 4");
            }
        }

        /// <summary>FillChunksToMesh prioritizes chunks with HasPlayerEdit over closer chunks.</summary>
        [Test]
        public void FillChunksToMesh_PrioritizesPlayerEdits()
        {
            List<int3> playerCoords = new() { int3.zero };

            // Create 10 close chunks without player edits
            for (int i = 0; i < 10; i++)
            {
                ManagedChunk chunk = CreateChunk(new int3(i, 0, 0));
                _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            }

            // Create 1 distant chunk with HasPlayerEdit
            ManagedChunk editedChunk = CreateChunk(new int3(50, 0, 0));
            editedChunk.HasPlayerEdit = true;
            _stateIndex.SetChunkState(editedChunk, ChunkState.Generated);

            _stateIndex.FillChunksToMesh(_result, 3, int3.zero, new float3(0, 0, 1), playerCoords);

            Assert.AreEqual(3, _result.Count);

            bool containsEdited = false;

            for (int i = 0; i < _result.Count; i++)
            {
                if (_result[i] == editedChunk)
                {
                    containsEdited = true;
                    break;
                }
            }

            Assert.IsTrue(containsEdited, "Edited chunk should be in top 3 despite being far away");
        }

        /// <summary>MarkNeedsLightUpdate adds to dirty set; ClearNeedsLightUpdate removes it.</summary>
        [Test]
        public void MarkNeedsLightUpdate_AndClear()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            Dictionary<int3, ManagedChunk> lookup = new() { { int3.zero, chunk } };

            _stateIndex.SetChunkState(chunk, ChunkState.Generated);
            _stateIndex.MarkNeedsLightUpdate(int3.zero, chunk);

            _stateIndex.FillChunksNeedingLightUpdate(_result, coord => lookup.GetValueOrDefault(coord));

            Assert.AreEqual(1, _result.Count);

            _stateIndex.ClearNeedsLightUpdate(int3.zero, chunk);

            _stateIndex.FillChunksNeedingLightUpdate(_result, coord => lookup.GetValueOrDefault(coord));

            Assert.AreEqual(0, _result.Count);
        }

        /// <summary>Clear empties all secondary indices and resets GeneratedChunkCount.</summary>
        [Test]
        public void Clear_EmptiesAllIndices()
        {
            ManagedChunk c1 = CreateChunk(new int3(0, 0, 0));
            ManagedChunk c2 = CreateChunk(new int3(1, 0, 0));
            ManagedChunk c3 = CreateChunk(new int3(2, 0, 0));

            _stateIndex.SetChunkState(c1, ChunkState.Generated);
            _stateIndex.SetChunkState(c2, ChunkState.Ready);
            _stateIndex.SetChunkState(c3, ChunkState.RelightPending);
            _stateIndex.MarkNeedsLightUpdate(new int3(0, 0, 0), c1);

            _stateIndex.Clear();

            _stateIndex.FillGeneratedChunks(_result);
            Assert.AreEqual(0, _result.Count, "Generated should be empty after Clear");

            _stateIndex.FillReadyChunks(_result);
            Assert.AreEqual(0, _result.Count, "Ready should be empty after Clear");

            _stateIndex.FillChunksNeedingRelight(_result);
            Assert.AreEqual(0, _result.Count, "RelightPending should be empty after Clear");

            Assert.AreEqual(0, _stateIndex.GeneratedChunkCount);
        }

        /// <summary>Creates a ManagedChunk at the given coordinate from the pool, tracking it for cleanup.</summary>
        private ManagedChunk CreateChunk(int3 coord)
        {
            NativeArray<StateId> data = _pool.Checkout();
            ManagedChunk chunk = new(coord, data);
            _createdChunks.Add(chunk);
            return chunk;
        }
    }
}
