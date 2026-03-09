using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Voxel.Block;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Loads BlockDefinitions from JSON files in the content directory.
    /// Discovers files at: {contentRoot}/assets/{namespace}/data/block/*.json
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class BlockDefinitionLoader
    {
        private readonly ILogger _logger;
        private readonly ContentValidator _validator;

        private static readonly string[] _validRenderLayers = { "opaque", "cutout", "translucent" };
        private static readonly string[] _validCollisionShapes = { "full_cube", "none", "slab", "stairs", "fence" };

        public BlockDefinitionLoader(ILogger logger, ContentValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }

        /// <summary>
        /// Loads all block definitions from the content root directory.
        /// Returns a list of successfully loaded definitions.
        /// Skips files with errors, logging each one.
        /// </summary>
        public List<BlockDefinition> LoadAll(string contentRoot)
        {
            List<BlockDefinition> definitions = new List<BlockDefinition>();
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
                string blocksDir = Path.Combine(namespaceDirs[i], "data", "block");

                if (!Directory.Exists(blocksDir))
                {
                    continue;
                }

                string[] jsonFiles = Directory.GetFiles(blocksDir, "*.json");

                for (int j = 0; j < jsonFiles.Length; j++)
                {
                    string filePath = jsonFiles[j];
                    string blockName = Path.GetFileNameWithoutExtension(filePath);
                    ResourceId id = new ResourceId(ns, blockName);

                    BlockDefinition definition = LoadSingle(filePath, id);

                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            return definitions;
        }

        /// <summary>
        /// Loads a single block definition from a JSON file.
        /// Returns null if the file is invalid.
        /// </summary>
        public BlockDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in '{filePath}': {ex.Message}");

                return null;
            }

            ValidationResult validation = new ValidationResult();

            // Parse properties
            List<PropertyDefinition> properties = new List<PropertyDefinition>();
            JToken propsToken = root["properties"];

            if (propsToken is JObject propsObj)
            {
                foreach (KeyValuePair<string, JToken> prop in propsObj)
                {
                    PropertyDefinition propDef = ParseProperty(prop.Key, prop.Value, id.ToString(), validation);

                    if (propDef != null)
                    {
                        properties.Add(propDef);
                    }
                }
            }

            BlockDefinition definition = new BlockDefinition(id, properties);

            // Parse scalar fields
            JToken hardnessToken = root["hardness"];

            if (hardnessToken != null)
            {
                definition.Hardness = hardnessToken.Value<double>();
            }

            JToken blastToken = root["blast_resistance"];

            if (blastToken != null)
            {
                definition.BlastResistance = blastToken.Value<double>();
            }

            JToken toolToken = root["requires_tool"];

            if (toolToken != null)
            {
                definition.RequiresTool = toolToken.Value<bool>();
            }

            JToken soundToken = root["sound_group"];

            if (soundToken != null)
            {
                definition.SoundGroup = soundToken.Value<string>();
            }

            JToken collisionToken = root["collision_shape"];

            if (collisionToken != null)
            {
                string collisionValue = collisionToken.Value<string>();

                _validator.ValidateEnumField(validation, "collision_shape", collisionValue, _validCollisionShapes, id.ToString());

                definition.CollisionShape = collisionValue;
            }

            JToken renderToken = root["render_layer"];

            if (renderToken != null)
            {
                string renderValue = renderToken.Value<string>();

                _validator.ValidateEnumField(validation, "render_layer", renderValue, _validRenderLayers, id.ToString());

                definition.RenderLayer = renderValue;
            }

            JToken emissionToken = root["light_emission"];

            if (emissionToken != null)
            {
                int emission = emissionToken.Value<int>();

                _validator.ValidateRange(validation, "light_emission", emission, 0, 15, id.ToString());

                definition.LightEmission = emission;
            }

            JToken filterToken = root["light_filter"];

            if (filterToken != null)
            {
                int filter = filterToken.Value<int>();

                _validator.ValidateRange(validation, "light_filter", filter, 0, 15, id.ToString());

                definition.LightFilter = filter;
            }

            JToken mapColorToken = root["map_color"];

            if (mapColorToken != null)
            {
                definition.MapColor = mapColorToken.Value<string>();
            }

            JToken lootToken = root["loot_table"];

            if (lootToken != null)
            {
                definition.LootTable = lootToken.Value<string>();
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

            // Report validation
            if (!validation.IsValid)
            {
                for (int i = 0; i < validation.Errors.Count; i++)
                {
                    _logger.LogError(validation.Errors[i]);
                }

                return null;
            }

            for (int i = 0; i < validation.Warnings.Count; i++)
            {
                _logger.LogWarning(validation.Warnings[i]);
            }

            return definition;
        }

        private static PropertyDefinition ParseProperty(
            string name,
            JToken value,
            string context,
            ValidationResult validation)
        {
            if (value is not JObject propObj)
            {
                validation.AddError($"[{context}] Property '{name}' must be a JSON object.");
                return null;
            }

            string type = propObj["type"]?.Value<string>();

            if (string.IsNullOrEmpty(type))
            {
                validation.AddError($"[{context}] Property '{name}' is missing 'type' field.");
                return null;
            }

            string defaultValue = propObj["default"]?.Value<string>();

            if (string.Equals(type, "bool", StringComparison.Ordinal))
            {
                bool defaultBool = string.Equals(defaultValue, "true", StringComparison.OrdinalIgnoreCase);
                return PropertyDefinition.Bool(name, defaultBool);
            }

            if (string.Equals(type, "enum", StringComparison.Ordinal))
            {
                JArray valuesArray = propObj["values"] as JArray;

                if (valuesArray == null || valuesArray.Count == 0)
                {
                    validation.AddError($"[{context}] Enum property '{name}' must have 'values' array.");
                    return null;
                }

                List<string> values = new List<string>();

                for (int i = 0; i < valuesArray.Count; i++)
                {
                    values.Add(valuesArray[i].Value<string>());
                }

                if (string.IsNullOrEmpty(defaultValue))
                {
                    defaultValue = values[0];
                }

                return PropertyDefinition.Enum(name, values, defaultValue);
            }

            if (string.Equals(type, "int", StringComparison.Ordinal))
            {
                int min = propObj["min"]?.Value<int>() ?? 0;
                int max = propObj["max"]?.Value<int>() ?? 0;

                if (min > max)
                {
                    validation.AddError(
                        $"[{context}] Int property '{name}' has min ({min}) > max ({max}).");

                    return null;
                }

                int defaultInt = 0;

                if (!string.IsNullOrEmpty(defaultValue))
                {
                    int.TryParse(defaultValue, out defaultInt);
                }

                return PropertyDefinition.IntRange(name, min, max, defaultInt);
            }

            validation.AddError($"[{context}] Unknown property type '{type}' for '{name}'.");

            return null;
        }
    }
}
