using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    public sealed class OreDefinitionLoader
    {
        private readonly ILogger _logger;

        public OreDefinitionLoader(ILogger logger)
        {
            _logger = logger;
        }

        public List<OreDefinition> LoadAll(string contentRoot)
        {
            List<OreDefinition> definitions = new List<OreDefinition>();
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
                string oresDir = Path.Combine(namespaceDirs[i], "data", "worldgen", "ore");

                if (!Directory.Exists(oresDir))
                {
                    continue;
                }

                string[] jsonFiles = Directory.GetFiles(oresDir, "*.json");

                for (int j = 0; j < jsonFiles.Length; j++)
                {
                    string filePath = jsonFiles[j];
                    string oreName = Path.GetFileNameWithoutExtension(filePath);
                    ResourceId id = new ResourceId(ns, oreName);

                    OreDefinition definition = LoadSingle(filePath, id);

                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            return definitions;
        }

        private OreDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read ore '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in ore '{filePath}': {ex.Message}");

                return null;
            }

            OreDefinition definition = new OreDefinition(id);

            string oreBlock = root["ore_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(oreBlock))
            {
                definition.OreBlock = ResourceId.Parse(oreBlock);
            }

            string replaceBlock = root["replace_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(replaceBlock))
            {
                definition.ReplaceBlock = ResourceId.Parse(replaceBlock);
            }

            definition.MinY = root["min_y"]?.Value<int>() ?? 0;
            definition.MaxY = root["max_y"]?.Value<int>() ?? 128;
            definition.VeinSize = root["vein_size"]?.Value<int>() ?? 8;
            definition.Frequency = root["frequency"]?.Value<float>() ?? 1.0f;
            definition.OreType = root["ore_type"]?.Value<string>() ?? "blob";

            return definition;
        }
    }
}
