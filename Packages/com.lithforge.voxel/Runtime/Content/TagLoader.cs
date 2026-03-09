using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Tag;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Loads TagDefinitions from JSON files in the content directory.
    /// Discovers files at: {contentRoot}/assets/{namespace}/data/tags/{category}/*.json
    /// Supports recursive subdirectories for tag categories (blocks, items, etc).
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class TagLoader
    {
        private readonly ILogger _logger;

        public TagLoader(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads all tag definitions from the content root directory.
        /// Returns a list of successfully loaded definitions.
        /// </summary>
        public List<TagDefinition> LoadAll(string contentRoot)
        {
            List<TagDefinition> definitions = new List<TagDefinition>();
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
                string tagsDir = Path.Combine(namespaceDirs[i], "data", "tags");

                if (!Directory.Exists(tagsDir))
                {
                    continue;
                }

                LoadDirectory(tagsDir, ns, "", definitions);
            }

            return definitions;
        }

        private void LoadDirectory(
            string directory,
            string ns,
            string pathPrefix,
            List<TagDefinition> definitions)
        {
            string[] jsonFiles = Directory.GetFiles(directory, "*.json");

            for (int i = 0; i < jsonFiles.Length; i++)
            {
                string filePath = jsonFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string idName = string.IsNullOrEmpty(pathPrefix)
                    ? fileName
                    : pathPrefix + "/" + fileName;
                ResourceId id = new ResourceId(ns, idName);

                TagDefinition definition = LoadSingle(filePath, id);

                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            string[] subDirs = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirs.Length; i++)
            {
                string subDirName = Path.GetFileName(subDirs[i]);
                string newPrefix = string.IsNullOrEmpty(pathPrefix)
                    ? subDirName
                    : pathPrefix + "/" + subDirName;
                LoadDirectory(subDirs[i], ns, newPrefix, definitions);
            }
        }

        private TagDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read tag '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in tag '{filePath}': {ex.Message}");

                return null;
            }

            TagDefinition definition = new TagDefinition(id);
            definition.Replace = root["replace"]?.Value<bool>() ?? false;

            JArray valuesArray = root["values"] as JArray;

            if (valuesArray != null)
            {
                for (int i = 0; i < valuesArray.Count; i++)
                {
                    string value = valuesArray[i].Value<string>();

                    if (!string.IsNullOrEmpty(value))
                    {
                        definition.Values.Add(value);
                    }
                }
            }

            return definition;
        }
    }
}
