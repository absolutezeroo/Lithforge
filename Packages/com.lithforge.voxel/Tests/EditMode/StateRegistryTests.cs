using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using NUnit.Framework;
using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class StateRegistryTests
    {
        [Test]
        public void Constructor_AirIsStateIdZero()
        {
            StateRegistry registry = new StateRegistry();

            BlockStateCompact airState = registry.GetState(StateId.Air);

            Assert.IsTrue(airState.IsAir);
            Assert.AreEqual(1, registry.TotalStateCount);
        }

        [Test]
        public void Register_NoProperties_ProducesOneState()
        {
            StateRegistry registry = new StateRegistry();

            ResourceId id = new ResourceId("lithforge", "stone");
            BlockDefinition stone = new BlockDefinition(id, new List<PropertyDefinition>());

            ushort baseId = registry.Register(stone);

            Assert.AreEqual(1, baseId); // 0 is AIR
            Assert.AreEqual(2, registry.TotalStateCount); // AIR + stone
        }

        [Test]
        public void Register_OakLog_ProducesThreeStates()
        {
            StateRegistry registry = new StateRegistry();

            ResourceId id = new ResourceId("lithforge", "oak_log");
            List<PropertyDefinition> props = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("axis", new List<string> { "x", "y", "z" }, "y")
            };
            BlockDefinition oakLog = new BlockDefinition(id, props);

            ushort baseId = registry.Register(oakLog);

            Assert.AreEqual(1, baseId);
            // AIR(0) + oak_log_x(1) + oak_log_y(2) + oak_log_z(3)
            Assert.AreEqual(4, registry.TotalStateCount);
        }

        [Test]
        public void Register_Furnace_ProducesEightStates()
        {
            StateRegistry registry = new StateRegistry();

            ResourceId id = new ResourceId("lithforge", "furnace");
            List<PropertyDefinition> props = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("facing",
                    new List<string> { "north", "south", "east", "west" }, "north"),
                PropertyDefinition.Bool("lit", false)
            };
            BlockDefinition furnace = new BlockDefinition(id, props);

            ushort baseId = registry.Register(furnace);

            Assert.AreEqual(1, baseId);
            // AIR(0) + 8 furnace states (4 facing × 2 lit)
            Assert.AreEqual(9, registry.TotalStateCount);
        }

        [Test]
        public void Register_MultipleBlocks_SequentialStateIds()
        {
            StateRegistry registry = new StateRegistry();

            // stone: 1 state
            ResourceId stoneId = new ResourceId("lithforge", "stone");
            BlockDefinition stone = new BlockDefinition(stoneId, new List<PropertyDefinition>());
            ushort stoneBase = registry.Register(stone);

            // oak_log: 3 states
            ResourceId logId = new ResourceId("lithforge", "oak_log");
            List<PropertyDefinition> logProps = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("axis", new List<string> { "x", "y", "z" }, "y")
            };
            BlockDefinition oakLog = new BlockDefinition(logId, logProps);
            ushort logBase = registry.Register(oakLog);

            // furnace: 8 states
            ResourceId furnaceId = new ResourceId("lithforge", "furnace");
            List<PropertyDefinition> furnaceProps = new List<PropertyDefinition>
            {
                PropertyDefinition.Enum("facing",
                    new List<string> { "north", "south", "east", "west" }, "north"),
                PropertyDefinition.Bool("lit", false)
            };
            BlockDefinition furnace = new BlockDefinition(furnaceId, furnaceProps);
            ushort furnaceBase = registry.Register(furnace);

            Assert.AreEqual(1, stoneBase);   // after AIR(0)
            Assert.AreEqual(2, logBase);     // after stone(1)
            Assert.AreEqual(5, furnaceBase); // after oak_log(2,3,4)
            Assert.AreEqual(13, registry.TotalStateCount); // 1 + 1 + 3 + 8
        }

        [Test]
        public void GetState_AfterRegister_ReturnsCachedFlags()
        {
            StateRegistry registry = new StateRegistry();

            ResourceId id = new ResourceId("lithforge", "stone");
            BlockDefinition stone = new BlockDefinition(id, new List<PropertyDefinition>())
            {
                RenderLayer = "opaque",
                CollisionShape = "full_cube",
                LightEmission = 0,
                LightFilter = 15
            };

            ushort baseId = registry.Register(stone);
            BlockStateCompact state = registry.GetState(new StateId(baseId));

            Assert.IsTrue(state.IsOpaque);
            Assert.IsTrue(state.IsFullCube);
            Assert.IsFalse(state.IsAir);
            Assert.IsFalse(state.EmitsLight);
            Assert.AreEqual(15, state.LightFilter);
        }

        [Test]
        public void Entries_TracksAllRegisteredBlocks()
        {
            StateRegistry registry = new StateRegistry();

            ResourceId id1 = new ResourceId("lithforge", "stone");
            ResourceId id2 = new ResourceId("lithforge", "dirt");
            registry.Register(new BlockDefinition(id1, new List<PropertyDefinition>()));
            registry.Register(new BlockDefinition(id2, new List<PropertyDefinition>()));

            Assert.AreEqual(2, registry.Entries.Count);
            Assert.AreEqual(id1, registry.Entries[0].Definition.Id);
            Assert.AreEqual(id2, registry.Entries[1].Definition.Id);
        }
    }
}
