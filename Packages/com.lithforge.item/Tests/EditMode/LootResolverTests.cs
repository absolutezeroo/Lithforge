using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item.Loot;
using Lithforge.Voxel.Loot;

using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class LootResolverTests
    {
        private static readonly ResourceId s_stoneTableId = new("lithforge", "blocks/stone");
        private static readonly ResourceId s_cobblestoneId = new("lithforge", "cobblestone");
        private static readonly ResourceId s_diamondId = new("lithforge", "diamond");
        private static readonly ResourceId s_gravelTableId = new("lithforge", "blocks/gravel");
        private static readonly ResourceId s_flintId = new("lithforge", "flint");
        private static readonly ResourceId s_gravelId = new("lithforge", "gravel");

        [Test]
        public void Resolve_SimpleItemDrop_ReturnsSingleItem()
        {
            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:cobblestone", Weight = 1,
            };

            LootPool pool = new();
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_stoneTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(s_cobblestoneId, drops[0].ItemId);
            Assert.AreEqual(1, drops[0].Count);
        }

        [Test]
        public void Resolve_UnknownTable_ReturnsEmptyList()
        {
            Dictionary<ResourceId, LootTableDefinition> tables = new();

            LootResolver resolver = new(tables);
            Random random = new(42);

            ResourceId unknownId = new("lithforge", "blocks/unknown");
            List<LootDrop> drops = resolver.Resolve(unknownId, random);

            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void Resolve_EmptyEntry_ProducesNoDrop()
        {
            LootEntry entry = new()
            {
                Type = "empty", Weight = 1,
            };

            LootPool pool = new();
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_stoneTableId, random);

            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void Resolve_SetCountFunction_AppliesCount()
        {
            LootFunction setCount = new()
            {
                Type = "set_count",
                Parameters = new Dictionary<string, string>
                {
                    {
                        "count", "3"
                    },
                },
            };
            setCount.PreParseValues();

            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:diamond", Weight = 1,
            };
            entry.Functions.Add(setCount);

            LootPool pool = new();
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_stoneTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(s_diamondId, drops[0].ItemId);
            Assert.AreEqual(3, drops[0].Count);
        }

        [Test]
        public void Resolve_SetCountRange_ReturnsWithinRange()
        {
            LootFunction setCount = new()
            {
                Type = "set_count",
                Parameters = new Dictionary<string, string>
                {
                    {
                        "min", "1"
                    },
                    {
                        "max", "4"
                    },
                },
            };
            setCount.PreParseValues();

            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:flint", Weight = 1,
            };
            entry.Functions.Add(setCount);

            LootPool pool = new();
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_gravelTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_gravelTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_gravelTableId, random);

            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(s_flintId, drops[0].ItemId);
            Assert.GreaterOrEqual(drops[0].Count, 1);
            Assert.LessOrEqual(drops[0].Count, 4);
        }

        [Test]
        public void Resolve_MultipleRolls_ProducesMultipleDrops()
        {
            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:cobblestone", Weight = 1,
            };

            LootPool pool = new()
            {
                RollsMin = 3, RollsMax = 3,
            };
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_stoneTableId, random);

            Assert.AreEqual(3, drops.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                Assert.AreEqual(s_cobblestoneId, drops[i].ItemId);
            }
        }

        [Test]
        public void Resolve_WeightedEntries_SelectsByWeight()
        {
            LootEntry heavyEntry = new()
            {
                Type = "item", Name = "lithforge:gravel", Weight = 100,
            };

            LootEntry lightEntry = new()
            {
                Type = "item", Name = "lithforge:flint", Weight = 1,
            };

            LootPool pool = new()
            {
                RollsMin = 10, RollsMax = 10,
            };
            pool.Entries.Add(heavyEntry);
            pool.Entries.Add(lightEntry);

            LootTableDefinition table = new(s_gravelTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_gravelTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> drops = resolver.Resolve(s_gravelTableId, random);

            Assert.AreEqual(10, drops.Count);

            // With weight 100 vs 1, most drops should be gravel
            int gravelCount = 0;

            for (int i = 0; i < drops.Count; i++)
            {
                if (drops[i].ItemId == s_gravelId)
                {
                    gravelCount++;
                }
            }

            Assert.GreaterOrEqual(gravelCount, 7, "Most drops should be the heavily-weighted item");
        }

        [Test]
        public void Resolve_ReturnedList_ReusedBetweenCalls()
        {
            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:cobblestone", Weight = 1,
            };

            LootPool pool = new();
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);
            Random random = new(42);

            List<LootDrop> first = resolver.Resolve(s_stoneTableId, random);
            List<LootDrop> second = resolver.Resolve(s_stoneTableId, random);

            // Both calls return the same list instance (reused)
            Assert.AreSame(first, second);
        }

        /// <summary>
        ///     Same seed produces identical drops. This verifies LootResolver is deterministic:
        ///     given the same Random seed, Resolve returns the same sequence of drops.
        ///     Note: EvaluateConditions is always true (no condition evaluation implemented).
        /// </summary>
        [Test]
        public void Resolve_SameSeed_ProducesSameDrops()
        {
            LootEntry entry = new()
            {
                Type = "item", Name = "lithforge:cobblestone", Weight = 1,
            };

            LootPool pool = new()
            {
                RollsMin = 3, RollsMax = 3,
            };
            pool.Entries.Add(entry);

            LootTableDefinition table = new(s_stoneTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_stoneTableId, table
                    },
                };

            LootResolver resolver = new(tables);

            // First call with seed 12345
            Random random1 = new(12345);
            List<LootDrop> drops1 = resolver.Resolve(s_stoneTableId, random1);
            int count1 = drops1.Count;
            ResourceId[] ids1 = new ResourceId[count1];
            int[] counts1 = new int[count1];

            for (int i = 0; i < count1; i++)
            {
                ids1[i] = drops1[i].ItemId;
                counts1[i] = drops1[i].Count;
            }

            // Second call with same seed 12345
            Random random2 = new(12345);
            List<LootDrop> drops2 = resolver.Resolve(s_stoneTableId, random2);

            Assert.AreEqual(count1, drops2.Count, "Same seed should produce same number of drops");

            for (int i = 0; i < count1; i++)
            {
                Assert.AreEqual(ids1[i], drops2[i].ItemId, $"Drop {i} item should match");
                Assert.AreEqual(counts1[i], drops2[i].Count, $"Drop {i} count should match");
            }
        }

        /// <summary>
        ///     Same seed determinism with weighted random selection across multiple entries.
        ///     Note: EvaluateConditions always returns true — no conditions are evaluated.
        /// </summary>
        [Test]
        public void Resolve_SameSeed_WeightedEntries_Deterministic()
        {
            LootEntry gravelEntry = new()
            {
                Type = "item", Name = "lithforge:gravel", Weight = 50,
            };

            LootEntry flintEntry = new()
            {
                Type = "item", Name = "lithforge:flint", Weight = 50,
            };

            LootPool pool = new()
            {
                RollsMin = 5, RollsMax = 5,
            };
            pool.Entries.Add(gravelEntry);
            pool.Entries.Add(flintEntry);

            LootTableDefinition table = new(s_gravelTableId);
            table.Pools.Add(pool);

            Dictionary<ResourceId, LootTableDefinition> tables =
                new()
                {
                    {
                        s_gravelTableId, table
                    },
                };

            LootResolver resolver = new(tables);

            // First resolve
            Random random1 = new(99999);
            List<LootDrop> drops1 = resolver.Resolve(s_gravelTableId, random1);
            ResourceId[] ids1 = new ResourceId[drops1.Count];

            for (int i = 0; i < drops1.Count; i++)
            {
                ids1[i] = drops1[i].ItemId;
            }

            // Second resolve with same seed
            Random random2 = new(99999);
            List<LootDrop> drops2 = resolver.Resolve(s_gravelTableId, random2);

            Assert.AreEqual(ids1.Length, drops2.Count);

            for (int i = 0; i < ids1.Length; i++)
            {
                Assert.AreEqual(ids1[i], drops2[i].ItemId,
                    $"Drop {i} should be deterministic with same seed");
            }
        }

        [Test]
        public void LootFunction_PreParseValues_ParsesAllFields()
        {
            LootFunction func = new()
            {
                Parameters = new Dictionary<string, string>
                {
                    {
                        "min", "2"
                    },
                    {
                        "max", "5"
                    },
                    {
                        "count", "10"
                    },
                },
            };
            func.PreParseValues();

            Assert.AreEqual(2, func.MinValue);
            Assert.AreEqual(5, func.MaxValue);
            Assert.AreEqual(10, func.CountValue);
        }

        [Test]
        public void LootFunction_PreParseValues_InvalidStrings_KeepDefaults()
        {
            LootFunction func = new()
            {
                Parameters = new Dictionary<string, string>
                {
                    {
                        "min", "not_a_number"
                    },
                    {
                        "max", ""
                    },
                },
            };
            func.PreParseValues();

            Assert.AreEqual(int.MinValue, func.MinValue);
            Assert.AreEqual(int.MinValue, func.MaxValue);
            Assert.AreEqual(int.MinValue, func.CountValue);
        }
    }
}
