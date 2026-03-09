using System;
using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// Resolves a loot table into concrete item drops.
    /// Evaluates conditions, selects entries by weight, and applies functions.
    /// </summary>
    public sealed class LootResolver
    {
        private readonly Dictionary<ResourceId, LootTableDefinition> _tables;

        public LootResolver(Dictionary<ResourceId, LootTableDefinition> tables)
        {
            _tables = tables;
        }

        /// <summary>
        /// Resolves a loot table by its resource id, returning a list of (itemId, count) drops.
        /// Returns an empty list if the table is not found.
        /// </summary>
        public List<LootDrop> Resolve(ResourceId tableId, Random random)
        {
            List<LootDrop> drops = new List<LootDrop>();

            if (!_tables.TryGetValue(tableId, out LootTableDefinition table))
            {
                return drops;
            }

            for (int p = 0; p < table.Pools.Count; p++)
            {
                LootPool pool = table.Pools[p];

                // Evaluate pool-level conditions
                if (!EvaluateConditions(pool.Conditions))
                {
                    continue;
                }

                // Determine roll count
                int rolls = pool.RollsMin;

                if (pool.RollsMax > pool.RollsMin)
                {
                    rolls = pool.RollsMin + random.Next(pool.RollsMax - pool.RollsMin + 1);
                }

                for (int r = 0; r < rolls; r++)
                {
                    LootEntry selected = SelectEntry(pool.Entries, random);

                    if (selected == null)
                    {
                        continue;
                    }

                    if (string.Equals(selected.Type, "empty", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(selected.Type, "item", StringComparison.Ordinal))
                    {
                        int count = 1;
                        count = ApplyFunctions(selected.Functions, count, random);

                        if (count > 0 && !string.IsNullOrEmpty(selected.Name))
                        {
                            ResourceId itemId = ResourceId.Parse(selected.Name);
                            drops.Add(new LootDrop(itemId, count));
                        }
                    }
                }
            }

            return drops;
        }

        private LootEntry SelectEntry(List<LootEntry> entries, Random random)
        {
            if (entries.Count == 0)
            {
                return null;
            }

            // Filter by conditions
            List<LootEntry> valid = new List<LootEntry>();
            int totalWeight = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                LootEntry entry = entries[i];

                if (EvaluateConditions(entry.Conditions))
                {
                    valid.Add(entry);
                    totalWeight += entry.Weight;
                }
            }

            if (valid.Count == 0 || totalWeight <= 0)
            {
                return null;
            }

            // Weighted random selection
            int roll = random.Next(totalWeight);
            int cumulative = 0;

            for (int i = 0; i < valid.Count; i++)
            {
                cumulative += valid[i].Weight;

                if (roll < cumulative)
                {
                    return valid[i];
                }
            }

            return valid[valid.Count - 1];
        }

        private bool EvaluateConditions(List<LootCondition> conditions)
        {
            // For Sprint 4: conditions are parsed and stored but always evaluate to true.
            // Full condition evaluation (silk_touch, match_tool, random_chance)
            // will be implemented when the tool system is integrated.
            return true;
        }

        private int ApplyFunctions(List<LootFunction> functions, int baseCount, Random random)
        {
            int count = baseCount;

            for (int i = 0; i < functions.Count; i++)
            {
                LootFunction func = functions[i];

                if (string.Equals(func.Type, "set_count", StringComparison.Ordinal))
                {
                    if (func.Parameters.TryGetValue("min", out string minStr) &&
                        func.Parameters.TryGetValue("max", out string maxStr))
                    {
                        int min = int.Parse(minStr);
                        int max = int.Parse(maxStr);
                        count = min + random.Next(max - min + 1);
                    }
                    else if (func.Parameters.TryGetValue("count", out string countStr))
                    {
                        count = int.Parse(countStr);
                    }
                }
            }

            return count;
        }
    }
}
