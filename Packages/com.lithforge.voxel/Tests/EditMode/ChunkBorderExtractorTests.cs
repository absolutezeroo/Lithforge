using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using NUnit.Framework;
using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkBorderExtractorTests
    {
        private NativeArray<StateId> _chunkData;
        private NativeArray<StateId> _output;

        [SetUp]
        public void SetUp()
        {
            _chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            _output = new NativeArray<StateId>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_chunkData.IsCreated) { _chunkData.Dispose(); }
            if (_output.IsCreated) { _output.Dispose(); }
        }

        [Test]
        public void ExtractPosX_ReturnsX31Plane()
        {
            StateId stone = new StateId(1);

            // Fill entire x=31 plane with stone
            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    _chunkData[ChunkData.GetIndex(31, y, z)] = stone;
                }
            }

            ChunkBorderExtractor.ExtractBorder(_chunkData, 0, _output);

            // Face 0 (+X): x=31, u=z, v=y
            for (int i = 0; i < ChunkConstants.SizeSquared; i++)
            {
                Assert.AreEqual(stone.Value, _output[i].Value,
                    $"Border element {i} should be stone");
            }
        }

        [Test]
        public void ExtractNegX_ReturnsX0Plane()
        {
            StateId stone = new StateId(1);

            // Fill only x=0, y=0, z=0
            _chunkData[ChunkData.GetIndex(0, 0, 0)] = stone;

            ChunkBorderExtractor.ExtractBorder(_chunkData, 1, _output);

            // Face 1 (-X): x=0, u=z, v=y → output[0*32+0] = (y=0, z=0)
            Assert.AreEqual(stone.Value, _output[0].Value);

            // All others should be air
            for (int i = 1; i < ChunkConstants.SizeSquared; i++)
            {
                Assert.AreEqual(0, _output[i].Value,
                    $"Border element {i} should be air");
            }
        }

        [Test]
        public void ExtractPosY_ReturnsY31Plane()
        {
            StateId stone = new StateId(1);

            // Place stone at x=5, y=31, z=10
            _chunkData[ChunkData.GetIndex(5, 31, 10)] = stone;

            ChunkBorderExtractor.ExtractBorder(_chunkData, 2, _output);

            // Face 2 (+Y): y=31, u=x, v=z → output[10*32+5]
            Assert.AreEqual(stone.Value, _output[10 * ChunkConstants.Size + 5].Value);

            // Spot-check that (0,0) is air
            Assert.AreEqual(0, _output[0].Value);
        }

        [Test]
        public void AirChunk_AllZeroBorders()
        {
            ChunkBorderExtractor.ExtractBorder(_chunkData, 4, _output);

            for (int i = 0; i < ChunkConstants.SizeSquared; i++)
            {
                Assert.AreEqual(0, _output[i].Value);
            }
        }
    }
}
