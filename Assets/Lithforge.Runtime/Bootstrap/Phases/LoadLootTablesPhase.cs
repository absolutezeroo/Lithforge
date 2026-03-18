using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item.Loot;
using Lithforge.Runtime.Content.Loot;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadLootTablesPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading loot tables...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            LootTable[] lootTableAssets = Resources.LoadAll<LootTable>("Content/LootTables");
            Dictionary<ResourceId, LootTableDefinition> lootTables = new();

            for (int i = 0; i < lootTableAssets.Length; i++)
            {
                LootTable lt = lootTableAssets[i];
                ResourceId ltId = new(lt.Namespace, lt.TableName);
                LootTableDefinition ltDef = ConvertLootTable(lt, ltId);
                lootTables[ltId] = ltDef;
            }

            ctx.LootTables = lootTables;
            ctx.Logger.LogInfo($"Loaded {lootTables.Count} loot tables.");
        }

        private static LootTableDefinition ConvertLootTable(LootTable lt, ResourceId id)
        {
            LootTableDefinition def = new(id)
            {
                Type = lt.Type,
            };

            IReadOnlyList<LootPoolEntry> pools = lt.Pools;

            for (int p = 0; p < pools.Count; p++)
            {
                LootPoolEntry poolEntry = pools[p];
                LootPool pool = new()
                {
                    RollsMin = poolEntry.RollsMin, RollsMax = poolEntry.RollsMax,
                };

                IReadOnlyList<LootItemEntry> items = poolEntry.Entries;

                for (int e = 0; e < items.Count; e++)
                {
                    LootItemEntry itemEntry = items[e];
                    LootEntry entry = new()
                    {
                        Type = itemEntry.Type, Name = itemEntry.ItemName, Weight = itemEntry.Weight,
                    };

                    IReadOnlyList<LootFunctionEntry> funcs = itemEntry.Functions;

                    for (int f = 0; f < funcs.Count; f++)
                    {
                        LootFunctionEntry funcEntry = funcs[f];
                        LootFunction func = new()
                        {
                            Type = funcEntry.FunctionType,
                        };

                        IReadOnlyList<StringPair> pars = funcEntry.Parameters;

                        for (int pi = 0; pi < pars.Count; pi++)
                        {
                            func.Parameters[pars[pi].Key] = pars[pi].Value;
                        }

                        func.PreParseValues();
                        entry.Functions.Add(func);
                    }

                    pool.Entries.Add(entry);
                }

                def.Pools.Add(pool);
            }

            return def;
        }
    }
}
