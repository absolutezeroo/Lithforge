using NUnit.Framework;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Physics.Tests
{
    [TestFixture]
    public sealed class VoxelColliderJobTests
    {
        /// <summary>
        ///     Builds a SolidBlockQuery with a solid floor at the given Y level.
        ///     All blocks at floorY are solid; all others are air.
        ///     Covers the broad-phase region for the given entity state.
        /// </summary>
        private static SolidBlockQuery BuildFloorQuery(
            float3 position,
            float3 velocity,
            float halfWidth,
            float height,
            int floorY)
        {
            VoxelCollider.ComputeBroadPhaseBounds(
                position, velocity, halfWidth, height,
                out int3 bpMin, out int3 bpMax);

            int sizeX = bpMax.x - bpMin.x + 1;
            int sizeY = bpMax.y - bpMin.y + 1;
            int sizeZ = bpMax.z - bpMin.z + 1;
            int volume = sizeX * sizeY * sizeZ;

            NativeHashMap<int3, bool> solidMap = new(volume, Allocator.TempJob);

            for (int x = bpMin.x; x <= bpMax.x; x++)
            {
                for (int y = bpMin.y; y <= bpMax.y; y++)
                {
                    for (int z = bpMin.z; z <= bpMax.z; z++)
                    {
                        int3 coord = new(x, y, z);
                        solidMap.TryAdd(coord, y == floorY);
                    }
                }
            }

            return new SolidBlockQuery
            {
                SolidMap = solidMap,
            };
        }

        [Test]
        public void FourEntities_FallOntoFloor_AllOnGround()
        {
            // Arrange: 4 entities at different XZ positions, all slightly above y=1
            // with downward velocity. Floor is solid at y=0.
            int entityCount = 4;
            NativeArray<EntityPhysicsState> states = new(entityCount, Allocator.TempJob);
            NativeArray<SolidBlockQuery> queries = new(entityCount, Allocator.TempJob);

            float halfWidth = 0.3f;
            float height = 1.8f;

            for (int i = 0; i < entityCount; i++)
            {
                float3 position = new(i * 5f + 0.5f, 1.05f, 0.5f);
                float3 velocity = new(0f, -0.5f, 0f);

                states[i] = new EntityPhysicsState
                {
                    Position = position, Velocity = velocity, HalfWidth = halfWidth, Height = height,
                };

                queries[i] = BuildFloorQuery(position, velocity, halfWidth, height, 0);
            }

            // Act
            VoxelColliderJob job = new()
            {
                EntityStates = states, Queries = queries,
            };
            job.Run(entityCount);

            // Assert
            for (int i = 0; i < entityCount; i++)
            {
                EntityPhysicsState result = states[i];
                Assert.IsTrue(result.Result.OnGround,
                    $"Entity {i} should be on ground");
                Assert.GreaterOrEqual(result.Position.y, 1f,
                    $"Entity {i} should be at or above the floor surface (y=1)");
                Assert.AreEqual(0f, result.Velocity.y, 0.001f,
                    $"Entity {i} vertical velocity should be zeroed");
            }

            // Cleanup
            for (int i = 0; i < entityCount; i++)
            {
                queries[i].SolidMap.Dispose();
            }

            queries.Dispose();
            states.Dispose();
        }

        [Test]
        public void Entity_InOpenAir_NotOnGround()
        {
            // Arrange: entity in open air with no solid blocks nearby
            NativeArray<EntityPhysicsState> states = new(1, Allocator.TempJob);
            NativeArray<SolidBlockQuery> queries = new(1, Allocator.TempJob);

            float halfWidth = 0.3f;
            float height = 1.8f;
            float3 position = new(0.5f, 10f, 0.5f);
            float3 velocity = new(0f, -0.5f, 0f);

            states[0] = new EntityPhysicsState
            {
                Position = position, Velocity = velocity, HalfWidth = halfWidth, Height = height,
            };

            // Build query with no solid blocks (floor at y=-100, outside broad-phase)
            VoxelCollider.ComputeBroadPhaseBounds(
                position, velocity, halfWidth, height,
                out int3 bpMin, out int3 bpMax);

            int sizeX = bpMax.x - bpMin.x + 1;
            int sizeY = bpMax.y - bpMin.y + 1;
            int sizeZ = bpMax.z - bpMin.z + 1;
            int volume = sizeX * sizeY * sizeZ;

            NativeHashMap<int3, bool> solidMap = new(volume, Allocator.TempJob);

            for (int x = bpMin.x; x <= bpMax.x; x++)
            {
                for (int y = bpMin.y; y <= bpMax.y; y++)
                {
                    for (int z = bpMin.z; z <= bpMax.z; z++)
                    {
                        solidMap.TryAdd(new int3(x, y, z), false);
                    }
                }
            }

            queries[0] = new SolidBlockQuery
            {
                SolidMap = solidMap,
            };

            // Act
            VoxelColliderJob job = new()
            {
                EntityStates = states, Queries = queries,
            };
            job.Run(1);

            // Assert
            EntityPhysicsState result = states[0];
            Assert.IsFalse(result.Result.OnGround, "Entity in open air should not be on ground");
            Assert.Less(result.Position.y, position.y,
                "Entity should have moved downward");

            // Cleanup
            queries[0].SolidMap.Dispose();
            queries.Dispose();
            states.Dispose();
        }

        [Test]
        public void Entity_HitsWall_VelocityZeroed()
        {
            // Arrange: entity moving +X into a solid wall at x=3
            NativeArray<EntityPhysicsState> states = new(1, Allocator.TempJob);
            NativeArray<SolidBlockQuery> queries = new(1, Allocator.TempJob);

            float halfWidth = 0.3f;
            float height = 1.8f;
            float3 position = new(2.5f, 1f, 0.5f);
            float3 velocity = new(0.5f, 0f, 0f);

            states[0] = new EntityPhysicsState
            {
                Position = position, Velocity = velocity, HalfWidth = halfWidth, Height = height,
            };

            // Build query: floor at y=0, wall at x=3 (all y levels in broad-phase)
            VoxelCollider.ComputeBroadPhaseBounds(
                position, velocity, halfWidth, height,
                out int3 bpMin, out int3 bpMax);

            int sizeX = bpMax.x - bpMin.x + 1;
            int sizeY = bpMax.y - bpMin.y + 1;
            int sizeZ = bpMax.z - bpMin.z + 1;
            int volume = sizeX * sizeY * sizeZ;

            NativeHashMap<int3, bool> solidMap = new(volume, Allocator.TempJob);

            for (int x = bpMin.x; x <= bpMax.x; x++)
            {
                for (int y = bpMin.y; y <= bpMax.y; y++)
                {
                    for (int z = bpMin.z; z <= bpMax.z; z++)
                    {
                        int3 coord = new(x, y, z);
                        bool solid = y == 0 || x == 3;
                        solidMap.TryAdd(coord, solid);
                    }
                }
            }

            queries[0] = new SolidBlockQuery
            {
                SolidMap = solidMap,
            };

            // Act
            VoxelColliderJob job = new()
            {
                EntityStates = states, Queries = queries,
            };
            job.Run(1);

            // Assert
            EntityPhysicsState result = states[0];
            Assert.IsTrue(result.Result.OnGround, "Entity should be on ground (floor at y=0)");
            Assert.AreEqual(0f, result.Velocity.x, 0.001f,
                "X velocity should be zeroed after hitting wall");
            Assert.Less(result.Position.x + halfWidth, 3f + 0.01f,
                "Entity should not penetrate the wall");

            // Cleanup
            queries[0].SolidMap.Dispose();
            queries.Dispose();
            states.Dispose();
        }

        [Test]
        public void ParallelSchedule_ProducesCorrectResults()
        {
            // Arrange: 8 entities, schedule with innerloopBatchCount=1
            int entityCount = 8;
            NativeArray<EntityPhysicsState> states = new(entityCount, Allocator.TempJob);
            NativeArray<SolidBlockQuery> queries = new(entityCount, Allocator.TempJob);

            float halfWidth = 0.3f;
            float height = 1.8f;

            for (int i = 0; i < entityCount; i++)
            {
                float3 position = new(i * 10f + 0.5f, 1.1f, 0.5f);
                float3 velocity = new(0f, -0.3f, 0f);

                states[i] = new EntityPhysicsState
                {
                    Position = position, Velocity = velocity, HalfWidth = halfWidth, Height = height,
                };

                queries[i] = BuildFloorQuery(position, velocity, halfWidth, height, 0);
            }

            // Act: schedule on worker threads
            VoxelColliderJob job = new()
            {
                EntityStates = states, Queries = queries,
            };
            JobHandle handle = job.Schedule(entityCount, 1);
            handle.Complete();

            // Assert
            for (int i = 0; i < entityCount; i++)
            {
                EntityPhysicsState result = states[i];
                Assert.IsTrue(result.Result.OnGround,
                    $"Entity {i} should be on ground after parallel schedule");
                Assert.GreaterOrEqual(result.Position.y, 1f,
                    $"Entity {i} position should be at or above floor");
            }

            // Cleanup
            for (int i = 0; i < entityCount; i++)
            {
                queries[i].SolidMap.Dispose();
            }

            queries.Dispose();
            states.Dispose();
        }
    }
}
