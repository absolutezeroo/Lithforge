using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using NUnit.Framework;
using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkSerializerTests
    {
        [Test]
        public void RoundTrip_UniformChunk_PreservesData()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Fill with single state
                StateId stoneId = new(1);

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    original[i] = stoneId;
                }

                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsTrue(success, "Deserialization should succeed");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(original[i], restored[i],
                        $"Voxel mismatch at index {i}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void RoundTrip_MixedChunk_PreservesAllStates()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Fill with various states
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    original[i] = new StateId((ushort)(i % 12));
                    originalLight[i] = (byte)(i % 256);
                }

                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsTrue(success, "Deserialization should succeed");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(original[i], restored[i],
                        $"Voxel mismatch at index {i}");
                    Assert.AreEqual(originalLight[i], restoredLight[i],
                        $"Light mismatch at index {i}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void RoundTrip_LightData_PreservesNibbles()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Pack various sun/block light levels
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    byte sun = (byte)(i % 16);
                    byte block = (byte)((i / 16) % 16);
                    originalLight[i] = (byte)((sun << 4) | block);
                }

                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsTrue(success, "Deserialization should succeed");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(originalLight[i], restoredLight[i],
                        $"Light nibble mismatch at index {i}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void Deserialize_InvalidMagic_ReturnsFalse()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                byte[] badData = new byte[] { 0, 0, 0, 0, 1 };
                bool success = ChunkSerializer.Deserialize(badData, chunkData, lightData, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsFalse(success, "Invalid magic should return false");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
            }
        }

        [Test]
        public void RoundTrip_WithLightData_PreservesBitExact()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Fill with non-zero light and block data
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    original[i] = new StateId((ushort)(i % 5 + 1));
                    byte sun = (byte)((i * 7) % 16);
                    byte block = (byte)((i * 3) % 16);
                    originalLight[i] = (byte)((sun << 4) | block);
                }

                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsTrue(success, "Deserialization should succeed");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(original[i], restored[i],
                        $"Voxel mismatch at index {i}");
                    Assert.AreEqual(originalLight[i], restoredLight[i],
                        $"Light mismatch at index {i}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void RoundTrip_PreservesMultipleBlockTypes()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Fill with 10 different StateId values in a recognizable pattern
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    original[i] = new StateId((ushort)((i * 13 + 7) % 10));
                }

                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsTrue(success, "Deserialization should succeed");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(original[i], restored[i],
                        $"Block type mismatch at index {i}: expected {original[i].Value} got {restored[i].Value}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void Deserialize_CorruptedCrc_ReturnsFalse()
        {
            NativeArray<StateId> original = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> originalLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<StateId> restored = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> restoredLight = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);

                // Flip a byte in the middle of the payload to corrupt it
                int midpoint = serialized.Length / 2;
                serialized[midpoint] = (byte)(serialized[midpoint] ^ 0xFF);

                bool success = ChunkSerializer.Deserialize(serialized, restored, restoredLight, out System.Collections.Generic.Dictionary<int, Lithforge.Voxel.BlockEntity.IBlockEntity> _, null);

                Assert.IsFalse(success, "Corrupted CRC should cause deserialization to return false");
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void Serialize_Compression_SmallerThanRaw()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                // Uniform data should compress well
                byte[] serialized = ChunkSerializer.Serialize(chunkData, lightData);

                int rawSize = ChunkConstants.Volume * 2 + ChunkConstants.Volume; // StateId (ushort) + byte light
                Assert.Less(serialized.Length, rawSize,
                    "Serialized data should be smaller than raw data for uniform chunks");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
            }
        }
    }
}
