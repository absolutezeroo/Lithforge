using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class LightPropagationJobTests
    {
        private NativeArray<StateId> _chunkData;
        private NativeArray<BlockStateCompact> _stateTable;
        private NativeArray<byte> _lightData;

        [SetUp]
        public void SetUp()
        {
            _chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            _lightData = new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            // State 0 = air (transparent), State 1 = stone (opaque)
            _stateTable = new NativeArray<BlockStateCompact>(2, Allocator.TempJob);
            _stateTable[0] = new BlockStateCompact { Flags = 0 }; // air: transparent
            _stateTable[1] = new BlockStateCompact { Flags = BlockStateCompact.FlagOpaque }; // stone: opaque
        }

        [TearDown]
        public void TearDown()
        {
            if (_chunkData.IsCreated)
            {
                _chunkData.Dispose();
            }

            if (_stateTable.IsCreated)
            {
                _stateTable.Dispose();
            }

            if (_lightData.IsCreated)
            {
                _lightData.Dispose();
            }
        }

        [Test]
        public void Execute_SunlightPropagatesDownAtFull15()
        {
            // Seed top layer with sunlight 15
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int topIndex = ChunkData.GetIndex(x, ChunkConstants.Size - 1, z);
                    _lightData[topIndex] = LightUtils.Pack(15, 0);
                }
            }

            LightPropagationJob job = new LightPropagationJob
            {
                ChunkData = _chunkData,
                StateTable = _stateTable,
                LightData = _lightData,
            };

            job.Schedule().Complete();

            // Sunlight should propagate straight down at full 15 through air
            int testX = 5;
            int testZ = 5;

            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                int index = ChunkData.GetIndex(testX, y, testZ);
                byte sun = LightUtils.GetSunLight(_lightData[index]);
                Assert.AreEqual(15, sun, $"Sunlight at y={y} should be 15");
            }
        }

        [Test]
        public void Execute_SunlightBlockedByOpaqueBlock()
        {
            StateId stoneId = new StateId(1);

            // Place a stone block in the middle
            int blockY = 16;
            int testX = 5;
            int testZ = 5;
            int blockIndex = ChunkData.GetIndex(testX, blockY, testZ);
            _chunkData[blockIndex] = stoneId;

            // Seed top layer with sunlight 15
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int topIndex = ChunkData.GetIndex(x, ChunkConstants.Size - 1, z);
                    _lightData[topIndex] = LightUtils.Pack(15, 0);
                }
            }

            LightPropagationJob job = new LightPropagationJob
            {
                ChunkData = _chunkData,
                StateTable = _stateTable,
                LightData = _lightData,
            };

            job.Schedule().Complete();

            // Above stone: should be 15
            int aboveIndex = ChunkData.GetIndex(testX, blockY + 1, testZ);
            byte sunAbove = LightUtils.GetSunLight(_lightData[aboveIndex]);
            Assert.AreEqual(15, sunAbove, "Sunlight above stone should be 15");

            // Below stone: should be less than 15 (light comes from neighbors, attenuated)
            int belowIndex = ChunkData.GetIndex(testX, blockY - 1, testZ);
            byte sunBelow = LightUtils.GetSunLight(_lightData[belowIndex]);
            Assert.Less(sunBelow, 15, "Sunlight below stone should be less than 15");
        }

        [Test]
        public void Execute_BlockLightPropagatesFromSource()
        {
            // Place a light source (block light = 14) at center
            int cx = 16;
            int cy = 16;
            int cz = 16;
            int centerIndex = ChunkData.GetIndex(cx, cy, cz);
            _lightData[centerIndex] = LightUtils.Pack(0, 14);

            LightPropagationJob job = new LightPropagationJob
            {
                ChunkData = _chunkData,
                StateTable = _stateTable,
                LightData = _lightData,
            };

            job.Schedule().Complete();

            // Center should remain 14
            byte centerBlock = LightUtils.GetBlockLight(_lightData[centerIndex]);
            Assert.AreEqual(14, centerBlock, "Center block light should remain 14");

            // Immediate neighbor should be 13
            int neighborIndex = ChunkData.GetIndex(cx + 1, cy, cz);
            byte neighborBlock = LightUtils.GetBlockLight(_lightData[neighborIndex]);
            Assert.AreEqual(13, neighborBlock, "Adjacent block light should be 13");

            // 2 blocks away should be 12
            int farIndex = ChunkData.GetIndex(cx + 2, cy, cz);
            byte farBlock = LightUtils.GetBlockLight(_lightData[farIndex]);
            Assert.AreEqual(12, farBlock, "Block 2 away should have light 12");

            // Far away should be 0
            int veryFarIndex = ChunkData.GetIndex(cx + 14, cy, cz);
            byte veryFarBlock = LightUtils.GetBlockLight(_lightData[veryFarIndex]);
            Assert.AreEqual(0, veryFarBlock, "Block 14 away should have light 0");
        }

        [Test]
        public void Execute_BlockLightStoppedByOpaqueBlock()
        {
            StateId stoneId = new StateId(1);

            int cx = 16;
            int cy = 16;
            int cz = 16;

            // Place light source at center
            int centerIndex = ChunkData.GetIndex(cx, cy, cz);
            _lightData[centerIndex] = LightUtils.Pack(0, 14);

            // Place stone wall next to the light source in +X
            int wallIndex = ChunkData.GetIndex(cx + 1, cy, cz);
            _chunkData[wallIndex] = stoneId;

            LightPropagationJob job = new LightPropagationJob
            {
                ChunkData = _chunkData,
                StateTable = _stateTable,
                LightData = _lightData,
            };

            job.Schedule().Complete();

            // Behind the wall should have much less light than if the wall weren't there
            int behindWallIndex = ChunkData.GetIndex(cx + 2, cy, cz);
            byte behindBlock = LightUtils.GetBlockLight(_lightData[behindWallIndex]);

            // Without wall, this would be 12. With wall, light has to go around.
            Assert.Less(behindBlock, 12, "Light behind wall should be reduced");
        }
    }
}
