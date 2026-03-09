using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Discovers and parses blockstate JSON files from the content directory.
    /// Path convention: assets/{namespace}/blockstates/{id}.json
    /// </summary>
    public sealed class BlockstateLoader
    {
        private readonly ILogger _logger;

        public BlockstateLoader(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<ResourceId, BlockstateDefinition> LoadAll(string contentRoot)
        {
            Dictionary<ResourceId, BlockstateDefinition> result =
                new Dictionary<ResourceId, BlockstateDefinition>();

            string assetsDir = Path.Combine(contentRoot, "assets");

            if (!Directory.Exists(assetsDir))
            {
                _logger.LogWarning($"Assets directory not found: {assetsDir}");

                return result;
            }

            string[] namespaceDirs = Directory.GetDirectories(assetsDir);

            for (int i = 0; i < namespaceDirs.Length; i++)
            {
                string ns = Path.GetFileName(namespaceDirs[i]);
                string blockstatesDir = Path.Combine(namespaceDirs[i], "blockstates");

                if (!Directory.Exists(blockstatesDir))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(blockstatesDir, "*.json");

                for (int j = 0; j < files.Length; j++)
                {
                    BlockstateDefinition def = LoadSingle(files[j], ns);

                    if (def != null)
                    {
                        result[def.Id] = def;
                    }
                }
            }

            _logger.LogInfo($"Loaded {result.Count} blockstate definitions.");

            return result;
        }

        private BlockstateDefinition LoadSingle(string filePath, string ns)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            ResourceId id = new ResourceId(ns, fileName);

            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                _logger.LogError($"Failed to read blockstate file '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogError($"Failed to parse blockstate JSON '{filePath}': {ex.Message}");

                return null;
            }

            JObject variantsObj = root["variants"] as JObject;

            if (variantsObj == null)
            {
                _logger.LogError($"Blockstate '{id}' has no 'variants' object.");

                return null;
            }

            Dictionary<string, BlockstateVariant> variants = new Dictionary<string, BlockstateVariant>();

            foreach (KeyValuePair<string, JToken> kvp in variantsObj)
            {
                JObject variantObj = kvp.Value as JObject;

                if (variantObj == null)
                {
                    _logger.LogWarning($"Blockstate '{id}' variant '{kvp.Key}' is not an object.");

                    continue;
                }

                string modelStr = variantObj["model"]?.ToString();

                if (string.IsNullOrEmpty(modelStr))
                {
                    _logger.LogWarning($"Blockstate '{id}' variant '{kvp.Key}' has no 'model' field.");

                    continue;
                }

                ResourceId modelId = ResourceId.Parse(modelStr);
                BlockstateVariant variant = new BlockstateVariant(modelId);
                variant.RotationX = variantObj["x"]?.Value<int>() ?? 0;
                variant.RotationY = variantObj["y"]?.Value<int>() ?? 0;
                variant.Uvlock = variantObj["uvlock"]?.Value<bool>() ?? false;

                variants[kvp.Key] = variant;
            }

            return new BlockstateDefinition(id, variants);
        }
    }
}
