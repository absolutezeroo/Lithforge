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
    public sealed class BiomeDefinitionLoader
    {
        private readonly ILogger _logger;

        public BiomeDefinitionLoader(ILogger logger)
        {
            _logger = logger;
        }

        public List<BiomeDefinition> LoadAll(string contentRoot)
        {
            List<BiomeDefinition> definitions = new List<BiomeDefinition>();
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
                string biomesDir = Path.Combine(namespaceDirs[i], "data", "worldgen", "biome");

                if (!Directory.Exists(biomesDir))
                {
                    continue;
                }

                string[] jsonFiles = Directory.GetFiles(biomesDir, "*.json");

                for (int j = 0; j < jsonFiles.Length; j++)
                {
                    string filePath = jsonFiles[j];
                    string biomeName = Path.GetFileNameWithoutExtension(filePath);
                    ResourceId id = new ResourceId(ns, biomeName);

                    BiomeDefinition definition = LoadSingle(filePath, id);

                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            return definitions;
        }

        private BiomeDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read biome '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in biome '{filePath}': {ex.Message}");

                return null;
            }

            BiomeDefinition definition = new BiomeDefinition(id);

            definition.TemperatureMin = root["temperature_min"]?.Value<float>() ?? 0.0f;
            definition.TemperatureMax = root["temperature_max"]?.Value<float>() ?? 1.0f;
            definition.TemperatureCenter = root["temperature_center"]?.Value<float>() ?? 0.5f;
            definition.HumidityMin = root["humidity_min"]?.Value<float>() ?? 0.0f;
            definition.HumidityMax = root["humidity_max"]?.Value<float>() ?? 1.0f;
            definition.HumidityCenter = root["humidity_center"]?.Value<float>() ?? 0.5f;

            string topBlock = root["top_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(topBlock))
            {
                definition.TopBlock = ResourceId.Parse(topBlock);
            }

            string fillerBlock = root["filler_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(fillerBlock))
            {
                definition.FillerBlock = ResourceId.Parse(fillerBlock);
            }

            string stoneBlock = root["stone_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(stoneBlock))
            {
                definition.StoneBlock = ResourceId.Parse(stoneBlock);
            }

            string underwaterBlock = root["underwater_block"]?.Value<string>();

            if (!string.IsNullOrEmpty(underwaterBlock))
            {
                definition.UnderwaterBlock = ResourceId.Parse(underwaterBlock);
            }

            definition.FillerDepth = root["filler_depth"]?.Value<int>() ?? 3;
            definition.TreeDensity = root["tree_density"]?.Value<float>() ?? 0.0f;
            definition.HeightModifier = root["height_modifier"]?.Value<float>() ?? 0.0f;

            return definition;
        }
    }
}
