using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using NUnit.Framework;
using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class NativeStateRegistryTests
    {
        [Test]
        public void BakeNative_MatchesManagedRegistry()
        {
            StateRegistry managed = new StateRegistry();

            ResourceId stoneId = new ResourceId("lithforge", "stone");
            BlockDefinition stone = new BlockDefinition(stoneId, new List<PropertyDefinition>())
            {
                RenderLayer = "opaque",
                CollisionShape = "full_cube",
                MapColor = "#7F7F7F"
            };
            managed.Register(stone);

            ResourceId logId = new ResourceId("lithforge", "oak_log");
            List<PropertyDefinition> logProps = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("axis", new List<string> { "x", "y", "z" }, "y")
            };
            BlockDefinition oakLog = new BlockDefinition(logId, logProps);
            managed.Register(oakLog);

            NativeStateRegistry native = managed.BakeNative(Allocator.TempJob);

            try
            {
                // Length must match
                Assert.AreEqual(managed.TotalStateCount, native.Length);

                // AIR at index 0
                BlockStateCompact airState = native.GetState(StateId.Air);
                Assert.IsTrue(airState.IsAir);

                // Stone at index 1
                BlockStateCompact stoneState = native.GetState(new StateId(1));
                Assert.IsTrue(stoneState.IsOpaque);
                Assert.IsTrue(stoneState.IsFullCube);
                Assert.IsFalse(stoneState.IsAir);

                // All managed states match native states
                for (int i = 0; i < managed.TotalStateCount; i++)
                {
                    StateId sid = new StateId((ushort)i);
                    BlockStateCompact managedState = managed.GetState(sid);
                    BlockStateCompact nativeState = native.GetState(sid);

                    Assert.AreEqual(managedState.Flags, nativeState.Flags,
                        $"Flags mismatch at StateId({i})");
                    Assert.AreEqual(managedState.RenderLayer, nativeState.RenderLayer,
                        $"RenderLayer mismatch at StateId({i})");
                    Assert.AreEqual(managedState.LightEmission, nativeState.LightEmission,
                        $"LightEmission mismatch at StateId({i})");
                    Assert.AreEqual(managedState.LightFilter, nativeState.LightFilter,
                        $"LightFilter mismatch at StateId({i})");
                    Assert.AreEqual(managedState.MapColor, nativeState.MapColor,
                        $"MapColor mismatch at StateId({i})");
                }

                // Verify stone's MapColor parsed correctly (#7F7F7F -> 0x7F7F7FFF)
                BlockStateCompact stoneNative = native.GetState(new StateId(1));
                Assert.AreEqual(0x7F7F7FFF, stoneNative.MapColor);
            }
            finally
            {
                native.Dispose();
            }
        }

        [Test]
        public void BakeNative_FurnaceEightStates()
        {
            StateRegistry managed = new StateRegistry();

            ResourceId furnaceId = new ResourceId("lithforge", "furnace");
            List<PropertyDefinition> props = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("facing",
                    new List<string> { "north", "south", "east", "west" }, "north"),
                PropertyDefinition.Bool("lit", false)
            };
            BlockDefinition furnace = new BlockDefinition(furnaceId, props)
            {
                RenderLayer = "opaque",
                CollisionShape = "full_cube"
            };
            managed.Register(furnace);

            NativeStateRegistry native = managed.BakeNative(Allocator.TempJob);

            try
            {
                // AIR(0) + 8 furnace states = 9 total
                Assert.AreEqual(9, native.Length);

                // All 8 furnace states should be opaque full cubes
                for (ushort i = 1; i <= 8; i++)
                {
                    BlockStateCompact state = native.GetState(new StateId(i));
                    Assert.IsTrue(state.IsOpaque, $"StateId({i}) should be opaque");
                    Assert.IsTrue(state.IsFullCube, $"StateId({i}) should be full cube");
                    Assert.IsFalse(state.IsAir, $"StateId({i}) should not be air");
                }
            }
            finally
            {
                native.Dispose();
            }
        }

        [Test]
        public void Dispose_DisposesNativeArray()
        {
            StateRegistry managed = new StateRegistry();
            NativeStateRegistry native = managed.BakeNative(Allocator.TempJob);

            native.Dispose();

            Assert.IsFalse(native.States.IsCreated);
        }
    }
}
