using System;
using System.Collections.Generic;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using NUnit.Framework;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Tests
{
    /// <summary>Tests for <see cref="ChunkEditor"/> block read/write, deferred edits, and border dirtying.</summary>
    [TestFixture]
    public sealed class ChunkEditorTests
    {
        /// <summary>Pool for allocating chunk NativeArrays.</summary>
        private ChunkPool _pool;

        /// <summary>Lookup table mapping chunk coordinates to ManagedChunk instances.</summary>
        private Dictionary<int3, ManagedChunk> _chunks;

        /// <summary>The chunk editor under test.</summary>
        private ChunkEditor _editor;

        /// <summary>Tracks all created chunks for cleanup.</summary>
        private List<ManagedChunk> _createdChunks;

        /// <summary>Reusable list for collecting dirtied chunk coordinates.</summary>
        private List<int3> _dirtiedChunks;

        /// <summary>Allocates pool, chunk dictionary, editor, and scratch lists.</summary>
        [SetUp]
        public void SetUp()
        {
            _pool = new ChunkPool(32);
            _chunks = new Dictionary<int3, ManagedChunk>();
            _createdChunks = new List<ManagedChunk>();
            _dirtiedChunks = new List<int3>();

            _editor = new ChunkEditor(
                coord => _chunks.GetValueOrDefault(coord),
                (chunk, newState) => { chunk.State = newState; });
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

        /// <summary>GetBlock on a loaded chunk returns the correct StateId.</summary>
        [Test]
        public void GetBlock_LoadedChunk_ReturnsCorrectState()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Ready;

            // Write StateId(5) at local (3, 4, 5)
            int index = ChunkData.GetIndex(3, 4, 5);
            chunk.Data[index] = new StateId(5);

            int3 worldCoord = new(3, 4, 5);
            StateId result = _editor.GetBlock(worldCoord);

            Assert.AreEqual(new StateId(5), result);
        }

        /// <summary>GetBlock on an unloaded coordinate returns StateId.Air.</summary>
        [Test]
        public void GetBlock_UnloadedChunk_ReturnsAir()
        {
            int3 worldCoord = new(100, 100, 100);
            StateId result = _editor.GetBlock(worldCoord);

            Assert.AreEqual(StateId.Air, result);
        }

        /// <summary>SetBlock on a Ready chunk writes data immediately and transitions to RelightPending.</summary>
        [Test]
        public void SetBlock_NormalPath_WritesDataAndTransitionsToRelightPending()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Ready;

            int3 worldCoord = new(5, 10, 15);
            int index = ChunkData.GetIndex(5, 10, 15);

            _editor.SetBlock(worldCoord, new StateId(10), _dirtiedChunks);

            Assert.AreEqual(new StateId(10), chunk.Data[index]);
            Assert.AreEqual(ChunkState.RelightPending, chunk.State);
            Assert.Contains(int3.zero, _dirtiedChunks);
        }

        /// <summary>SetBlock on a Meshing chunk defers the edit instead of writing immediately.</summary>
        [Test]
        public void SetBlock_DeferredPath_DoesNotWriteImmediately()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Meshing;

            int3 worldCoord = new(5, 10, 15);
            int index = ChunkData.GetIndex(5, 10, 15);
            StateId originalState = chunk.Data[index];

            _editor.SetBlock(worldCoord, new StateId(10), _dirtiedChunks);

            Assert.AreEqual(originalState, chunk.Data[index], "Data should not be written while Meshing");
            Assert.AreEqual(1, chunk.DeferredEdits.Count);
            Assert.AreEqual(new StateId(10), chunk.DeferredEdits[0].NewState);
        }

        /// <summary>ApplyDeferredEdits writes all deferred edits to chunk data and clears the list.</summary>
        [Test]
        public void ApplyDeferredEdits_WritesAllEdits()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Meshing;

            // Queue 3 deferred edits
            _editor.SetBlock(new int3(1, 0, 0), new StateId(10), _dirtiedChunks);
            _editor.SetBlock(new int3(2, 0, 0), new StateId(11), _dirtiedChunks);
            _editor.SetBlock(new int3(3, 0, 0), new StateId(12), _dirtiedChunks);

            Assert.AreEqual(3, chunk.DeferredEdits.Count);

            // Simulate mesh job completion
            chunk.State = ChunkState.Generated;

            _editor.ApplyDeferredEdits(chunk);

            Assert.AreEqual(new StateId(10), chunk.Data[ChunkData.GetIndex(1, 0, 0)]);
            Assert.AreEqual(new StateId(11), chunk.Data[ChunkData.GetIndex(2, 0, 0)]);
            Assert.AreEqual(new StateId(12), chunk.Data[ChunkData.GetIndex(3, 0, 0)]);
            Assert.AreEqual(0, chunk.DeferredEdits.Count);
            Assert.AreEqual(ChunkState.RelightPending, chunk.State);
        }

        /// <summary>SetBlock fires the OnBlockChanged callback with the correct world coordinate and state.</summary>
        [Test]
        public void SetBlock_FiresOnBlockChanged()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Ready;

            int3 capturedCoord = default;
            StateId capturedState = default;
            int callCount = 0;

            _editor.OnBlockChanged = (coord, state) =>
            {
                capturedCoord = coord;
                capturedState = state;
                callCount++;
            };

            int3 worldCoord = new(7, 8, 9);
            _editor.SetBlock(worldCoord, new StateId(42), _dirtiedChunks);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(new int3(7, 8, 9), capturedCoord);
            Assert.AreEqual(new StateId(42), capturedState);
        }

        /// <summary>SetBlock on a border voxel (localX=0) dirties the -X neighbor chunk.</summary>
        [Test]
        public void SetBlock_BorderEdit_DirtiesNeighbor()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Ready;

            ManagedChunk neighbor = CreateChunk(new int3(-1, 0, 0));
            neighbor.State = ChunkState.Ready;

            // Link neighbors for ReadyNeighborMask (not strictly required for border dirty logic)
            // The editor uses _getChunk to find the neighbor.

            // Edit at localX=0 (worldX=0 in chunk (0,0,0)) should dirty neighbor at (-1,0,0)
            int3 worldCoord = new(0, 5, 5);
            _editor.SetBlock(worldCoord, new StateId(7), _dirtiedChunks);

            // dirtiedChunks should contain both the edited chunk and the neighbor
            Assert.Contains(int3.zero, _dirtiedChunks);
            Assert.Contains(new int3(-1, 0, 0), _dirtiedChunks);

            // Neighbor should be transitioned to Generated (remesh)
            Assert.AreEqual(ChunkState.Generated, neighbor.State);
        }

        /// <summary>IsBlockLoaded returns true for a Ready chunk with valid data.</summary>
        [Test]
        public void IsBlockLoaded_ReturnsTrueForReadyChunk()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Ready;

            int3 worldCoord = new(5, 5, 5);
            bool loaded = _editor.IsBlockLoaded(worldCoord);

            Assert.IsTrue(loaded);
        }

        /// <summary>IsBlockLoaded returns false for a Generating chunk.</summary>
        [Test]
        public void IsBlockLoaded_ReturnsFalseForGeneratingChunk()
        {
            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.State = ChunkState.Generating;

            int3 worldCoord = new(5, 5, 5);
            bool loaded = _editor.IsBlockLoaded(worldCoord);

            Assert.IsFalse(loaded);
        }

        /// <summary>Creates a ManagedChunk at the given coordinate and registers it in the lookup table.</summary>
        private ManagedChunk CreateChunk(int3 coord)
        {
            NativeArray<StateId> data = _pool.Checkout();
            ManagedChunk chunk = new(coord, data);
            _chunks[coord] = chunk;
            _createdChunks.Add(chunk);
            return chunk;
        }
    }
}
