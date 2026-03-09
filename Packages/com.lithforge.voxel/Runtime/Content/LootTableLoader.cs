using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Loot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Loads LootTableDefinitions from JSON files in the content directory.
    /// Discovers files at: {contentRoot}/assets/{namespace}/data/loot_tables/**/*.json
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class LootTableLoader
    {
        private readonly ILogger _logger;

        public LootTableLoader(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads all loot table definitions from the content root directory.
        /// Returns a dictionary keyed by ResourceId for fast lookup.
        /// </summary>
        public Dictionary<ResourceId, LootTableDefinition> LoadAll(string contentRoot)
        {
            Dictionary<ResourceId, LootTableDefinition> tables =
                new Dictionary<ResourceId, LootTableDefinition>();
            string assetsDir = Path.Combine(contentRoot, "assets");

            if (!Directory.Exists(assetsDir))
            {
                _logger.LogWarning($"Assets directory not found: {assetsDir}");

                return tables;
            }

            string[] namespaceDirs = Directory.GetDirectories(assetsDir);

            for (int i = 0; i < namespaceDirs.Length; i++)
            {
                string ns = Path.GetFileName(namespaceDirs[i]);
                string lootDir = Path.Combine(namespaceDirs[i], "data", "loot_tables");

                if (!Directory.Exists(lootDir))
                {
                    continue;
                }

                ContentDirectoryScanner.Scan(lootDir, ns, "", (string filePath, ResourceId id) =>
                {
                    LootTableDefinition definition = LoadSingle(filePath, id);

                    if (definition != null)
                    {
                        tables[id] = definition;
                    }
                });
            }

            return tables;
        }

        private LootTableDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read loot table '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in loot table '{filePath}': {ex.Message}");

                return null;
            }

            LootTableDefinition definition = new LootTableDefinition(id);
            definition.Type = root["type"]?.Value<string>() ?? "block";

            JArray poolsArray = root["pools"] as JArray;

            if (poolsArray != null)
            {
                for (int i = 0; i < poolsArray.Count; i++)
                {
                    LootPool pool = ParsePool(poolsArray[i] as JObject, id);

                    if (pool != null)
                    {
                        definition.Pools.Add(pool);
                    }
                }
            }

            return definition;
        }

        private LootPool ParsePool(JObject poolObj, ResourceId context)
        {
            if (poolObj == null)
            {
                return null;
            }

            LootPool pool = new LootPool();

            // Parse rolls (can be int or {min, max} object)
            JToken rollsToken = poolObj["rolls"];

            if (rollsToken != null)
            {
                if (rollsToken.Type == JTokenType.Integer)
                {
                    int rolls = rollsToken.Value<int>();
                    pool.RollsMin = rolls;
                    pool.RollsMax = rolls;
                }
                else if (rollsToken.Type == JTokenType.Object)
                {
                    pool.RollsMin = rollsToken["min"]?.Value<int>() ?? 1;
                    pool.RollsMax = rollsToken["max"]?.Value<int>() ?? 1;
                }
            }

            // Parse entries
            JArray entriesArray = poolObj["entries"] as JArray;

            if (entriesArray != null)
            {
                for (int i = 0; i < entriesArray.Count; i++)
                {
                    LootEntry entry = ParseEntry(entriesArray[i] as JObject, context);

                    if (entry != null)
                    {
                        pool.Entries.Add(entry);
                    }
                }
            }

            // Parse pool-level conditions
            JArray conditionsArray = poolObj["conditions"] as JArray;

            if (conditionsArray != null)
            {
                for (int i = 0; i < conditionsArray.Count; i++)
                {
                    LootCondition condition = ParseCondition(conditionsArray[i] as JObject);

                    if (condition != null)
                    {
                        pool.Conditions.Add(condition);
                    }
                }
            }

            return pool;
        }

        private LootEntry ParseEntry(JObject entryObj, ResourceId context)
        {
            if (entryObj == null)
            {
                return null;
            }

            LootEntry entry = new LootEntry();
            entry.Type = entryObj["type"]?.Value<string>() ?? "item";
            entry.Name = entryObj["name"]?.Value<string>() ?? "";
            entry.Weight = entryObj["weight"]?.Value<int>() ?? 1;

            // Parse conditions
            JArray conditionsArray = entryObj["conditions"] as JArray;

            if (conditionsArray != null)
            {
                for (int i = 0; i < conditionsArray.Count; i++)
                {
                    LootCondition condition = ParseCondition(conditionsArray[i] as JObject);

                    if (condition != null)
                    {
                        entry.Conditions.Add(condition);
                    }
                }
            }

            // Parse functions
            JArray functionsArray = entryObj["functions"] as JArray;

            if (functionsArray != null)
            {
                for (int i = 0; i < functionsArray.Count; i++)
                {
                    LootFunction function = ParseFunction(functionsArray[i] as JObject);

                    if (function != null)
                    {
                        entry.Functions.Add(function);
                    }
                }
            }

            return entry;
        }

        private LootCondition ParseCondition(JObject condObj)
        {
            if (condObj == null)
            {
                return null;
            }

            LootCondition condition = new LootCondition();
            condition.Type = condObj["condition"]?.Value<string>() ?? "";

            // Store all other fields as string parameters
            foreach (KeyValuePair<string, JToken> prop in condObj)
            {
                if (string.Equals(prop.Key, "condition", StringComparison.Ordinal))
                {
                    continue;
                }

                condition.Parameters[prop.Key] = prop.Value.ToString();
            }

            return condition;
        }

        private LootFunction ParseFunction(JObject funcObj)
        {
            if (funcObj == null)
            {
                return null;
            }

            LootFunction function = new LootFunction();
            function.Type = funcObj["function"]?.Value<string>() ?? "";

            // Store all other fields as string parameters
            foreach (KeyValuePair<string, JToken> prop in funcObj)
            {
                if (string.Equals(prop.Key, "function", StringComparison.Ordinal))
                {
                    continue;
                }

                function.Parameters[prop.Key] = prop.Value.ToString();
            }

            return function;
        }
    }
}
