using System.Collections.Generic;

using Lithforge.Network.Chunk;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using NUnit.Framework;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Network.Tests
{
    /// <summary>Tests for <see cref="ChunkNetSerializer"/> round-trip serialization of full chunks and block change batches.</summary>
    [TestFixture]
    public sealed class ChunkNetSerializerTests
    {
        /// <summary>Source chunk voxel data for round-trip tests.</summary>
        private NativeArray<StateId> _chunkData;

        /// <summary>Source light data for round-trip tests.</summary>
        private NativeArray<byte> _lightData;

        /// <summary>Target chunk voxel data for deserialization output.</summary>
        private NativeArray<StateId> _targetData;

        /// <summary>Target light data for deserialization output.</summary>
        private NativeArray<byte> _targetLight;

        /// <summary>Allocates NativeArrays for round-trip testing.</summary>
        [SetUp]
        public void SetUp()
        {
            _chunkData = new NativeArray<StateId>(ChunkConstants.Volume, Allocator.TempJob);
            _lightData = new NativeArray<byte>(ChunkConstants.Volume, Allocator.TempJob);
            _targetData = new NativeArray<StateId>(ChunkConstants.Volume, Allocator.TempJob);
            _targetLight = new NativeArray<byte>(ChunkConstants.Volume, Allocator.TempJob);
        }

        /// <summary>Disposes all NativeArrays to prevent leaks.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_chunkData.IsCreated)
            {
                _chunkData.Dispose();
            }

            if (_lightData.IsCreated)
            {
                _lightData.Dispose();
            }

            if (_targetData.IsCreated)
            {
                _targetData.Dispose();
            }

            if (_targetLight.IsCreated)
            {
                _targetLight.Dispose();
            }

            ChunkNetSerializer.DisposeThreadLocalResources();
        }

        /// <summary>Round-trip of a uniform chunk (single palette entry) preserves all voxel and light data.</summary>
        [Test]
        public void FullChunk_RoundTrip_UniformData()
        {
            StateId stone = new(1);

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _chunkData[i] = stone;
                _lightData[i] = 0xFF;
            }

            byte[] serialized = ChunkNetSerializer.SerializeFullChunk(_chunkData, _lightData);

            Assert.IsNotNull(serialized);
            Assert.Greater(serialized.Length, 0);

            bool ok = ChunkNetSerializer.DeserializeFullChunk(serialized, _targetData, _targetLight);

            Assert.IsTrue(ok);

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                Assert.AreEqual(_chunkData[i], _targetData[i], $"Voxel mismatch at index {i}");
                Assert.AreEqual(_lightData[i], _targetLight[i], $"Light mismatch at index {i}");
            }
        }

        /// <summary>Round-trip of mixed data (banded Y pattern with varied light) preserves all values.</summary>
        [Test]
        public void FullChunk_RoundTrip_MixedData()
        {
            StateId air = new(0);
            StateId stone = new(1);
            StateId dirt = new(2);

            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                StateId state = (y % 3) switch
                {
                    0 => air,
                    1 => stone,
                    _ => dirt,
                };

                // Nibble-pack: sun=15 for Y>=16, sun=0 for Y<16
                byte sun = y >= 16 ? (byte)15 : (byte)0;
                byte light = (byte)(sun << 4 | 0x0F);

                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int index = y * ChunkConstants.SizeSquared + z * ChunkConstants.Size + x;
                        _chunkData[index] = state;
                        _lightData[index] = light;
                    }
                }
            }

            byte[] serialized = ChunkNetSerializer.SerializeFullChunk(_chunkData, _lightData);
            bool ok = ChunkNetSerializer.DeserializeFullChunk(serialized, _targetData, _targetLight);

            Assert.IsTrue(ok);

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                Assert.AreEqual(_chunkData[i], _targetData[i], $"Voxel mismatch at index {i}");
                Assert.AreEqual(_lightData[i], _targetLight[i], $"Light mismatch at index {i}");
            }
        }

        /// <summary>Round-trip of an all-air chunk (palette count=1, voxelByteCount=0) preserves values.</summary>
        [Test]
        public void FullChunk_RoundTrip_AllAir()
        {
            // NativeArray is zero-initialized, so chunkData is already all-air (StateId(0))
            // and lightData is already all 0.

            byte[] serialized = ChunkNetSerializer.SerializeFullChunk(_chunkData, _lightData);
            bool ok = ChunkNetSerializer.DeserializeFullChunk(serialized, _targetData, _targetLight);

            Assert.IsTrue(ok);

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                Assert.AreEqual(new StateId(0), _targetData[i], $"Voxel mismatch at index {i}");
                Assert.AreEqual(0, _targetLight[i], $"Light mismatch at index {i}");
            }
        }

        /// <summary>Deserializing garbage data with wrong magic bytes returns false.</summary>
        [Test]
        public void FullChunk_Deserialize_InvalidMagic_ReturnsFalse()
        {
            byte[] garbage = { 0, 0, 0, 0, 0 };

            bool ok = ChunkNetSerializer.DeserializeFullChunk(garbage, _targetData, _targetLight);

            Assert.IsFalse(ok);
        }

        /// <summary>Deserializing null data returns false without throwing.</summary>
        [Test]
        public void FullChunk_Deserialize_NullData_ReturnsFalse()
        {
            bool ok = ChunkNetSerializer.DeserializeFullChunk(null, _targetData, _targetLight);

            Assert.IsFalse(ok);
        }

        /// <summary>Deserializing truncated data returns false without throwing.</summary>
        [Test]
        public void FullChunk_Deserialize_TruncatedData_ReturnsFalse()
        {
            StateId stone = new(1);

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _chunkData[i] = stone;
                _lightData[i] = 0xFF;
            }

            byte[] serialized = ChunkNetSerializer.SerializeFullChunk(_chunkData, _lightData);
            int halfLength = serialized.Length / 2;
            byte[] truncated = new byte[halfLength];
            System.Array.Copy(serialized, truncated, halfLength);

            bool ok = ChunkNetSerializer.DeserializeFullChunk(truncated, _targetData, _targetLight);

            Assert.IsFalse(ok);
        }

        /// <summary>Single block change batch round-trips position and state correctly.</summary>
        [Test]
        public void BlockChangeBatch_RoundTrip_SingleChange()
        {
            int3 chunkCoord = new(1, 2, 3);
            List<BlockChangeEntry> changes = new()
            {
                new BlockChangeEntry { Position = new int3(32, 64, 96), NewState = new StateId(5) },
            };

            byte[] serialized = ChunkNetSerializer.SerializeBlockChangeBatch(chunkCoord, changes);

            Assert.IsNotNull(serialized);
            Assert.Greater(serialized.Length, 0);

            bool ok = ChunkNetSerializer.DeserializeBlockChangeBatch(serialized, out int3 outCoord, out List<BlockChangeEntry> outChanges);

            Assert.IsTrue(ok);
            Assert.AreEqual(new int3(1, 2, 3), outCoord);
            Assert.AreEqual(1, outChanges.Count);
            Assert.AreEqual(new int3(32, 64, 96), outChanges[0].Position);
            Assert.AreEqual(5, outChanges[0].NewState.Value);
        }

        /// <summary>Small batch (below compression threshold) round-trips all entries correctly.</summary>
        [Test]
        public void BlockChangeBatch_RoundTrip_SmallBatch()
        {
            int3 chunkCoord = new(0, 0, 0);
            List<BlockChangeEntry> changes = new();

            for (int i = 0; i < 5; i++)
            {
                changes.Add(new BlockChangeEntry
                {
                    Position = new int3(i, i + 1, i + 2),
                    NewState = new StateId((ushort)(i + 10)),
                });
            }

            byte[] serialized = ChunkNetSerializer.SerializeBlockChangeBatch(chunkCoord, changes);
            bool ok = ChunkNetSerializer.DeserializeBlockChangeBatch(serialized, out int3 outCoord, out List<BlockChangeEntry> outChanges);

            Assert.IsTrue(ok);
            Assert.AreEqual(int3.zero, outCoord);
            Assert.AreEqual(5, outChanges.Count);

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(new int3(i, i + 1, i + 2), outChanges[i].Position, $"Position mismatch at entry {i}");
                Assert.AreEqual((ushort)(i + 10), outChanges[i].NewState.Value, $"State mismatch at entry {i}");
            }
        }

        /// <summary>Large batch (above compression threshold) uses zstd compression and round-trips correctly.</summary>
        [Test]
        public void BlockChangeBatch_RoundTrip_LargeBatch_Compressed()
        {
            int3 chunkCoord = new(2, 1, 0);
            List<BlockChangeEntry> changes = new();

            for (int i = 0; i < 50; i++)
            {
                changes.Add(new BlockChangeEntry
                {
                    Position = new int3(
                        chunkCoord.x * ChunkConstants.Size + (i % ChunkConstants.Size),
                        chunkCoord.y * ChunkConstants.Size + ((i / ChunkConstants.Size) % ChunkConstants.Size),
                        chunkCoord.z * ChunkConstants.Size + (i / ChunkConstants.SizeSquared)),
                    NewState = new StateId((ushort)(100 + i)),
                });
            }

            byte[] serialized = ChunkNetSerializer.SerializeBlockChangeBatch(chunkCoord, changes);

            Assert.IsNotNull(serialized);
            Assert.Greater(serialized.Length, 0);

            bool ok = ChunkNetSerializer.DeserializeBlockChangeBatch(serialized, out int3 outCoord, out List<BlockChangeEntry> outChanges);

            Assert.IsTrue(ok);
            Assert.AreEqual(new int3(2, 1, 0), outCoord);
            Assert.AreEqual(50, outChanges.Count);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(changes[i].Position, outChanges[i].Position, $"Position mismatch at entry {i}");
                Assert.AreEqual(changes[i].NewState.Value, outChanges[i].NewState.Value, $"State mismatch at entry {i}");
            }
        }

        /// <summary>Serializing an empty change list returns an empty byte array.</summary>
        [Test]
        public void BlockChangeBatch_EmptyList_ReturnsEmpty()
        {
            byte[] serialized = ChunkNetSerializer.SerializeBlockChangeBatch(int3.zero, new List<BlockChangeEntry>());

            Assert.IsNotNull(serialized);
            Assert.AreEqual(0, serialized.Length);
        }

        /// <summary>Deserializing a too-short byte array returns false without throwing.</summary>
        [Test]
        public void BlockChangeBatch_Deserialize_TooShort_ReturnsFalse()
        {
            byte[] tooShort = new byte[5];

            bool ok = ChunkNetSerializer.DeserializeBlockChangeBatch(tooShort, out int3 _, out List<BlockChangeEntry> _);

            Assert.IsFalse(ok);
        }
    }
}
