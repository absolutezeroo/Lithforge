using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Crafting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Loads RecipeDefinitions from JSON files in the content directory.
    /// Discovers files at: {contentRoot}/assets/{namespace}/data/recipes/*.json
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class RecipeLoader
    {
        private readonly ILogger _logger;

        public RecipeLoader(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads all recipe definitions from the content root directory.
        /// </summary>
        public List<RecipeDefinition> LoadAll(string contentRoot)
        {
            List<RecipeDefinition> recipes = new List<RecipeDefinition>();
            string assetsDir = Path.Combine(contentRoot, "assets");

            if (!Directory.Exists(assetsDir))
            {
                _logger.LogWarning($"Assets directory not found: {assetsDir}");

                return recipes;
            }

            string[] namespaceDirs = Directory.GetDirectories(assetsDir);

            for (int i = 0; i < namespaceDirs.Length; i++)
            {
                string ns = Path.GetFileName(namespaceDirs[i]);
                string recipesDir = Path.Combine(namespaceDirs[i], "data", "recipes");

                if (!Directory.Exists(recipesDir))
                {
                    continue;
                }

                LoadDirectory(recipesDir, ns, "", recipes);
            }

            return recipes;
        }

        private void LoadDirectory(
            string directory,
            string ns,
            string pathPrefix,
            List<RecipeDefinition> recipes)
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

                RecipeDefinition recipe = LoadSingle(filePath, id);

                if (recipe != null)
                {
                    recipes.Add(recipe);
                }
            }

            string[] subDirs = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirs.Length; i++)
            {
                string subDirName = Path.GetFileName(subDirs[i]);
                string newPrefix = string.IsNullOrEmpty(pathPrefix)
                    ? subDirName
                    : pathPrefix + "/" + subDirName;
                LoadDirectory(subDirs[i], ns, newPrefix, recipes);
            }
        }

        private RecipeDefinition LoadSingle(string filePath, ResourceId id)
        {
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read recipe '{filePath}': {ex.Message}");

                return null;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parse error in recipe '{filePath}': {ex.Message}");

                return null;
            }

            RecipeDefinition recipe = new RecipeDefinition(id);

            string typeStr = root["type"]?.Value<string>() ?? "shaped";

            if (string.Equals(typeStr, "shapeless", StringComparison.Ordinal))
            {
                recipe.Type = RecipeType.Shapeless;
            }
            else
            {
                recipe.Type = RecipeType.Shaped;
            }

            // Parse result
            JObject resultObj = root["result"] as JObject;

            if (resultObj != null)
            {
                string resultItem = resultObj["item"]?.Value<string>();

                if (!string.IsNullOrEmpty(resultItem))
                {
                    recipe.ResultItem = ResourceId.Parse(resultItem);
                }

                recipe.ResultCount = resultObj["count"]?.Value<int>() ?? 1;
            }

            // Parse shaped pattern + key
            if (recipe.Type == RecipeType.Shaped)
            {
                JArray patternArray = root["pattern"] as JArray;

                if (patternArray != null)
                {
                    for (int i = 0; i < patternArray.Count; i++)
                    {
                        recipe.Pattern.Add(patternArray[i].Value<string>());
                    }
                }

                JObject keyObj = root["key"] as JObject;

                if (keyObj != null)
                {
                    foreach (KeyValuePair<string, JToken> kvp in keyObj)
                    {
                        if (kvp.Key.Length > 0)
                        {
                            char keyChar = kvp.Key[0];
                            string itemStr = null;

                            if (kvp.Value.Type == JTokenType.Object)
                            {
                                itemStr = kvp.Value["item"]?.Value<string>();
                            }
                            else if (kvp.Value.Type == JTokenType.String)
                            {
                                itemStr = kvp.Value.Value<string>();
                            }

                            if (!string.IsNullOrEmpty(itemStr))
                            {
                                recipe.Keys[keyChar] = ResourceId.Parse(itemStr);
                            }
                        }
                    }
                }
            }
            else
            {
                // Parse shapeless ingredients
                JArray ingredientsArray = root["ingredients"] as JArray;

                if (ingredientsArray != null)
                {
                    for (int i = 0; i < ingredientsArray.Count; i++)
                    {
                        JToken ingToken = ingredientsArray[i];
                        string itemStr = null;

                        if (ingToken.Type == JTokenType.Object)
                        {
                            itemStr = ingToken["item"]?.Value<string>();
                        }
                        else if (ingToken.Type == JTokenType.String)
                        {
                            itemStr = ingToken.Value<string>();
                        }

                        if (!string.IsNullOrEmpty(itemStr))
                        {
                            recipe.Ingredients.Add(ResourceId.Parse(itemStr));
                        }
                    }
                }
            }

            return recipe;
        }
    }
}
