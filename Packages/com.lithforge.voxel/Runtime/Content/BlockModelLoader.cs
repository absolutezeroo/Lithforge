using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Discovers and parses block model JSON files from the content directory.
    /// Path convention: assets/{namespace}/models/block/{id}.json
    /// Models contain a parent reference and texture variable mappings.
    /// </summary>
    public sealed class BlockModelLoader
    {
        private readonly ILogger _logger;

        public BlockModelLoader(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<ResourceId, BlockModel> LoadAll(string contentRoot)
        {
            Dictionary<ResourceId, BlockModel> result = new Dictionary<ResourceId, BlockModel>();

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
                string modelsDir = Path.Combine(namespaceDirs[i], "models", "block");

                if (!Directory.Exists(modelsDir))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(modelsDir, "*.json");

                for (int j = 0; j < files.Length; j++)
                {
                    BlockModel model = LoadSingle(files[j], ns);

                    if (model != null)
                    {
                        result[model.Id] = model;
                    }
                }
            }

            _logger.LogInfo($"Loaded {result.Count} block models.");
            return result;
        }

        private BlockModel LoadSingle(string filePath, string ns)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            ResourceId id = new ResourceId(ns, "block/" + fileName);

            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                _logger.LogError($"Failed to read model file '{filePath}': {ex.Message}");
                return null;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogError($"Failed to parse model JSON '{filePath}': {ex.Message}");
                return null;
            }

            BlockModel model = new BlockModel(id);
            model.Parent = root["parent"]?.ToString();

            JObject texturesObj = root["textures"] as JObject;

            if (texturesObj != null)
            {
                foreach (KeyValuePair<string, JToken> kvp in texturesObj)
                {
                    model.Textures[kvp.Key] = kvp.Value.ToString();
                }
            }

            return model;
        }
    }
}
