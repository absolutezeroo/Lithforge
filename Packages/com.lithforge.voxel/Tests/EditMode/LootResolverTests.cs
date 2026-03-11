using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Loot;
using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class LootResolverTests
    {
        private static readonly ResourceId _stoneTableId = new ResourceId("lithforge", "blocks/stone");
        private static readonly ResourceId _cobblestoneId = new ResourceId("lithforge", "cobblestone");
        private static readonly ResourceId _diamondId = new ResourceId("lithforge", "diamond");
        private static readonly ResourceId _gravelTableId = new ResourceId("lithforge", "blocks/gravel");
        private static readonly ResourceId _flintId = new ResourceId("lithforge", "flint");
        private static readonly ResourceId _gravelId = new ResourceId("lithforge", "gravel");

        [Test]
        public void Resolve_SimpleItemDrop_ReturnsSingleItem()
        {
            LootEntry entry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:cobblestone",
                Weight = 1
            };

            LootPool pool = new LootPool();
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _stoneTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_stoneTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(_cobblestoneId, drops[0].ItemId);
            Assert.AreEqual(1, drops[0].Count);
        }

        [Test]
        public void Resolve_UnknownTable_ReturnsEmptyList()
        {
            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition>();

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            ResourceId unknownId = new ResourceId("lithforge", "blocks/unknown");
            List<LootDrop> drops = resolver.Resolve(unknownId, random);

            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void Resolve_EmptyEntry_ProducesNoDrop()
        {
            LootEntry entry = new LootEntry
            {
                Type = "empty",
                Weight = 1
            };

            LootPool pool = new LootPool();
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _stoneTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_stoneTableId, random);

            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void Resolve_SetCountFunction_AppliesCount()
        {
            LootFunction setCount = new LootFunction();
            setCount.Type = "set_count";
            setCount.Parameters = new Dictionary<string, string>
            {
                { "count", "3" }
            };
            setCount.PreParseValues();

            LootEntry entry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:diamond",
                Weight = 1
            };
            entry.Functions.Add(setCount);

            LootPool pool = new LootPool();
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _stoneTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_stoneTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(_diamondId, drops[0].ItemId);
            Assert.AreEqual(3, drops[0].Count);
        }

        [Test]
        public void Resolve_SetCountRange_ReturnsWithinRange()
        {
            LootFunction setCount = new LootFunction();
            setCount.Type = "set_count";
            setCount.Parameters = new Dictionary<string, string>
            {
                { "min", "1" },
                { "max", "4" }
            };
            setCount.PreParseValues();

            LootEntry entry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:flint",
                Weight = 1
            };
            entry.Functions.Add(setCount);

            LootPool pool = new LootPool();
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_gravelTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _gravelTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_gravelTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(_flintId, drops[0].ItemId);
            Assert.GreaterOrEqual(drops[0].Count, 1);
            Assert.LessOrEqual(drops[0].Count, 4);
        }

        [Test]
        public void Resolve_MultipleRolls_ProducesMultipleDrops()
        {
            LootEntry entry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:cobblestone",
                Weight = 1
            };

            LootPool pool = new LootPool
            {
                RollsMin = 3,
                RollsMax = 3
            };
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _stoneTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_stoneTableId, random);

            Assert.AreEqual(3, drops.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                Assert.AreEqual(_cobblestoneId, drops[i].ItemId);
            }
        }

        [Test]
        public void Resolve_WeightedEntries_SelectsByWeight()
        {
            LootEntry heavyEntry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:gravel",
                Weight = 100
            };

            LootEntry lightEntry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:flint",
                Weight = 1
            };

            LootPool pool = new LootPool
            {
                RollsMin = 10,
                RollsMax = 10
            };
            pool.Entries.Add(heavyEntry);
            pool.Entries.Add(lightEntry);

            LootTableDefinition table = new LootTableDefinition(_gravelTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _gravelTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> drops = resolver.Resolve(_gravelTableId, random);

            Assert.AreEqual(10, drops.Count);

            // With weight 100 vs 1, most drops should be gravel
            int gravelCount = 0;

            for (int i = 0; i < drops.Count; i++)
            {
                if (drops[i].ItemId == _gravelId)
                {
                    gravelCount++;
                }
            }

            Assert.GreaterOrEqual(gravelCount, 7, "Most drops should be the heavily-weighted item");
        }

        [Test]
        public void Resolve_ReturnedList_ReusedBetweenCalls()
        {
            LootEntry entry = new LootEntry
            {
                Type = "item",
                Name = "lithforge:cobblestone",
                Weight = 1
            };

            LootPool pool = new LootPool();
            pool.Entries.Add(entry);

            LootTableDefinition table = new LootTableDefinition(_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition> { { _stoneTableId, table } };

            LootResolver resolver = new LootResolver(tables);
            Random random = new Random(42);

            List<LootDrop> first = resolver.Resolve(_stoneTableId, random);
            List<LootDrop> second = resolver.Resolve(_stoneTableId, random);

            // Both calls return the same list instance (reused)
            Assert.AreSame(first, second);
        }

        [Test]
        public void LootFunction_PreParseValues_ParsesAllFields()
        {
            LootFunction func = new LootFunction();
            func.Parameters = new Dictionary<string, string>
            {
                { "min", "2" },
                { "max", "5" },
                { "count", "10" }
            };
            func.PreParseValues();

            Assert.AreEqual(2, func.MinValue);
            Assert.AreEqual(5, func.MaxValue);
            Assert.AreEqual(10, func.CountValue);
        }

        [Test]
        public void LootFunction_PreParseValues_InvalidStrings_KeepDefaults()
        {
            LootFunction func = new LootFunction();
            func.Parameters = new Dictionary<string, string>
            {
                { "min", "not_a_number" },
                { "max", "" }
            };
            func.PreParseValues();

            Assert.AreEqual(int.MinValue, func.MinValue);
            Assert.AreEqual(int.MinValue, func.MaxValue);
            Assert.AreEqual(int.MinValue, func.CountValue);
        }
    }
}
