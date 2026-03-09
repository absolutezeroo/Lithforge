using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Item;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Loads ItemDefinitions from JSON files in the content directory.
    /// Discovers files at: {contentRoot}/assets/{namespace}/data/item/*.json
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class ItemDefinitionLoader
    {
        private readonly ILogger _logger;

        public ItemDefinitionLoader(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads all item definitions from the content root directory.
        /// Returns a list of successfully loaded definitions.
        /// </summary>
        public List<ItemDefinition> LoadAll(string contentRoot)
        {
            List<ItemDefinition> definitions = new List<ItemDefinition>();
            string assetsDir = Path.Combine(contentRoot, "assets");

            if (!Directory.Exists(assetsDir))
            {
                _logger.LogWarning($"Assets directory not found: {assetsDir}");

                return definitions;
            }

            string[] namespaceDirs = Directory.GetDirectories(assetsDir);

            for (int i = 0; i < namespaceDirs.Length; i++)
            {
                string ns = Path.GetFileName(namespaceDirs[i]);
                string itemsDir = Path.Combine(namespaceDirs[i], "data", "item");

                if (!Directory.Exists(itemsDir))
                {
                    continue;
                }

                string[] jsonFiles = Directory.GetFiles(itemsDir, "*.json");

                for (int j = 0; j < jsonFiles.Length; j++)
                {
                    string filePath = jsonFiles[j];
                    string itemName = Path.GetFileNameWithoutExtension(filePath);
                    ResourceId id = new ResourceId(ns, itemName);

                    ItemDefinition definition = LoadSingle(filePath, id);

                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            return definitions;
        }

        private ItemDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read item '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in item '{filePath}': {ex.Message}");

                return null;
            }

            ItemDefinition definition = new ItemDefinition(id);

            definition.MaxStackSize = root["max_stack_size"]?.Value<int>() ?? 64;
            definition.ToolLevel = root["tool_level"]?.Value<int>() ?? 0;
            definition.Durability = root["durability"]?.Value<int>() ?? 0;
            definition.AttackDamage = root["attack_damage"]?.Value<float>() ?? 1.0f;
            definition.AttackSpeed = root["attack_speed"]?.Value<float>() ?? 4.0f;
            definition.MiningSpeed = root["mining_speed"]?.Value<float>() ?? 1.0f;

            string toolTypeStr = root["tool_type"]?.Value<string>();

            if (!string.IsNullOrEmpty(toolTypeStr))
            {
                definition.ToolType = ParseToolType(toolTypeStr, id);
            }

            string blockIdStr = root["block_id"]?.Value<string>();

            if (!string.IsNullOrEmpty(blockIdStr))
            {
                definition.IsBlockItem = true;
                definition.BlockId = ResourceId.Parse(blockIdStr);
            }

            JToken tagsToken = root["tags"];

            if (tagsToken is JArray tagsArray)
            {
                List<string> tags = new List<string>();

                for (int i = 0; i < tagsArray.Count; i++)
                {
                    tags.Add(tagsArray[i].Value<string>());
                }

                definition.Tags = tags;
            }

            return definition;
        }

        private ToolType ParseToolType(string value, ResourceId context)
        {
            if (string.Equals(value, "pickaxe", StringComparison.Ordinal))
            {
                return ToolType.Pickaxe;
            }

            if (string.Equals(value, "axe", StringComparison.Ordinal))
            {
                return ToolType.Axe;
            }

            if (string.Equals(value, "shovel", StringComparison.Ordinal))
            {
                return ToolType.Shovel;
            }

            if (string.Equals(value, "hoe", StringComparison.Ordinal))
            {
                return ToolType.Hoe;
            }

            if (string.Equals(value, "sword", StringComparison.Ordinal))
            {
                return ToolType.Sword;
            }

            _logger.LogWarning(
                $"[{context}] Unknown tool_type '{value}', defaulting to None.");

            return ToolType.None;
        }
    }
}
