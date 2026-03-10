#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Runtime.Content;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    /// <summary>
    /// One-time migration script: reads all JSON content files from StreamingAssets/content/
    /// and creates equivalent ScriptableObject assets in Assets/Resources/Content/.
    /// Run via menu: Lithforge → Convert JSON to ScriptableObjects
    /// </summary>
    public static class JsonToSOConverter
    {
        private const string ContentRoot = "Assets/StreamingAssets/content/lithforge/assets/lithforge";
        private const string OutputRoot = "Assets/Resources/Content";

        private static readonly Dictionary<string, BlockModel> _modelCache =
            new Dictionary<string, BlockModel>();

        private static readonly Dictionary<string, BlockDefinition> _blockCache =
            new Dictionary<string, BlockDefinition>();

        private static readonly Dictionary<string, BlockStateMapping> _mappingCache =
            new Dictionary<string, BlockStateMapping>();

        private static readonly Dictionary<string, LootTable> _lootCache =
            new Dictionary<string, LootTable>();

        private static readonly Dictionary<string, ItemDefinition> _itemCache =
            new Dictionary<string, ItemDefinition>();

        [MenuItem("Lithforge/Convert JSON to ScriptableObjects")]
        public static void ConvertAll()
        {
            _modelCache.Clear();
            _blockCache.Clear();
            _mappingCache.Clear();
            _lootCache.Clear();
            _itemCache.Clear();

            EnsureDirectory(OutputRoot);
            EnsureDirectory(OutputRoot + "/Models");
            EnsureDirectory(OutputRoot + "/Blocks");
            EnsureDirectory(OutputRoot + "/BlockStates");
            EnsureDirectory(OutputRoot + "/Items");
            EnsureDirectory(OutputRoot + "/LootTables");
            EnsureDirectory(OutputRoot + "/Tags");
            EnsureDirectory(OutputRoot + "/Recipes");
            EnsureDirectory(OutputRoot + "/Biomes");
            EnsureDirectory(OutputRoot + "/Ores");

            EnsureDirectory(OutputRoot + "/ItemModels");

            // Order matters: models first, then blockstates, then blocks (which ref both)
            ConvertBlockModels();
            ConvertItemModels();
            ConvertBlockStates();
            ConvertLootTables();
            ConvertBlocks();
            ConvertItems();
            ConvertTags();
            ConvertRecipes();
            ConvertBiomes();
            ConvertOres();

            // Wire cross-references
            WireBlockReferences();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[JsonToSOConverter] Conversion complete. " +
                $"{_blockCache.Count} blocks, {_modelCache.Count} models, " +
                $"{_mappingCache.Count} mappings, {_itemCache.Count} items, " +
                $"{_lootCache.Count} loot tables.");
        }

        private static void ConvertBlockModels()
        {
            string modelsDir = Path.Combine(ContentRoot, "models", "block");

            if (!Directory.Exists(modelsDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(modelsDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string blockName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                BlockModel model = ScriptableObject.CreateInstance<BlockModel>();

                // Set built-in parent type
                string parentStr = root["parent"]?.Value<string>();
                SerializedObject so = new SerializedObject(model);

                if (IsBuiltInModel(parentStr))
                {
                    so.FindProperty("_builtInParent").enumValueIndex =
                        (int)ParseBuiltInParent(parentStr);
                }
                else if (!string.IsNullOrEmpty(parentStr))
                {
                    // Will wire parent refs after all models created
                }

                // Textures
                SerializedProperty texturesProp = so.FindProperty("_textures");
                JObject texturesObj = root["textures"] as JObject;

                if (texturesObj != null)
                {
                    foreach (KeyValuePair<string, JToken> kvp in texturesObj)
                    {
                        texturesProp.InsertArrayElementAtIndex(texturesProp.arraySize);
                        SerializedProperty entry = texturesProp.GetArrayElementAtIndex(
                            texturesProp.arraySize - 1);
                        entry.FindPropertyRelative("_variable").stringValue = kvp.Key;
                        entry.FindPropertyRelative("_value").stringValue = kvp.Value.ToString();
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Models/{blockName}.asset";
                AssetDatabase.CreateAsset(model, assetPath);
                _modelCache[blockName] = model;
            }

            // Wire parent references between models
            string[] allFiles = Directory.GetFiles(modelsDir, "*.json");

            for (int i = 0; i < allFiles.Length; i++)
            {
                string filePath = allFiles[i];
                string blockName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                string parentStr = root["parent"]?.Value<string>();

                if (!string.IsNullOrEmpty(parentStr) && !IsBuiltInModel(parentStr))
                {
                    string parentName = ExtractModelName(parentStr);

                    if (_modelCache.TryGetValue(parentName, out BlockModel parentModel) &&
                        _modelCache.TryGetValue(blockName, out BlockModel childModel))
                    {
                        SerializedObject so = new SerializedObject(childModel);
                        so.FindProperty("_parent").objectReferenceValue = parentModel;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }

        private static void ConvertItemModels()
        {
            string modelsDir = Path.Combine(ContentRoot, "models", "item");

            if (!Directory.Exists(modelsDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(modelsDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string itemName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                BlockModel model = ScriptableObject.CreateInstance<BlockModel>();
                SerializedObject so = new SerializedObject(model);

                // Parent reference
                string parentStr = root["parent"]?.Value<string>();

                if (IsBuiltInModel(parentStr))
                {
                    so.FindProperty("_builtInParent").enumValueIndex =
                        (int)ParseBuiltInParent(parentStr);
                }

                // Textures
                SerializedProperty texturesProp = so.FindProperty("_textures");
                JObject texturesObj = root["textures"] as JObject;

                if (texturesObj != null)
                {
                    foreach (KeyValuePair<string, JToken> kvp in texturesObj)
                    {
                        texturesProp.InsertArrayElementAtIndex(texturesProp.arraySize);
                        SerializedProperty entry = texturesProp.GetArrayElementAtIndex(
                            texturesProp.arraySize - 1);
                        entry.FindPropertyRelative("_variable").stringValue = kvp.Key;
                        entry.FindPropertyRelative("_value").stringValue = kvp.Value.ToString();
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/ItemModels/{itemName}.asset";
                AssetDatabase.CreateAsset(model, assetPath);
            }

            // Wire parent references between item models and block models
            string[] allFiles = Directory.GetFiles(modelsDir, "*.json");

            for (int i = 0; i < allFiles.Length; i++)
            {
                string filePath = allFiles[i];
                string itemName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                string parentStr = root["parent"]?.Value<string>();

                if (!string.IsNullOrEmpty(parentStr) && !IsBuiltInModel(parentStr))
                {
                    string parentName = ExtractModelName(parentStr);
                    string assetPath = $"{OutputRoot}/ItemModels/{itemName}.asset";
                    BlockModel childModel = AssetDatabase.LoadAssetAtPath<BlockModel>(assetPath);

                    if (childModel == null)
                    {
                        continue;
                    }

                    // Check item models first, then block models
                    string parentItemPath = $"{OutputRoot}/ItemModels/{parentName}.asset";
                    BlockModel parentModel = AssetDatabase.LoadAssetAtPath<BlockModel>(parentItemPath);

                    if (parentModel == null && _modelCache.TryGetValue(parentName, out BlockModel blockParent))
                    {
                        parentModel = blockParent;
                    }

                    if (parentModel != null)
                    {
                        SerializedObject so = new SerializedObject(childModel);
                        so.FindProperty("_parent").objectReferenceValue = parentModel;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }

        private static void ConvertBlockStates()
        {
            string blockstatesDir = Path.Combine(ContentRoot, "blockstates");

            if (!Directory.Exists(blockstatesDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(blockstatesDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string blockName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                BlockStateMapping mapping = ScriptableObject.CreateInstance<BlockStateMapping>();
                SerializedObject so = new SerializedObject(mapping);
                SerializedProperty variantsProp = so.FindProperty("_variants");

                JObject variantsObj = root["variants"] as JObject;

                if (variantsObj != null)
                {
                    foreach (KeyValuePair<string, JToken> kvp in variantsObj)
                    {
                        JObject variantObj = kvp.Value as JObject;

                        if (variantObj == null)
                        {
                            continue;
                        }

                        variantsProp.InsertArrayElementAtIndex(variantsProp.arraySize);
                        SerializedProperty entry = variantsProp.GetArrayElementAtIndex(
                            variantsProp.arraySize - 1);

                        entry.FindPropertyRelative("_variantKey").stringValue = kvp.Key;
                        entry.FindPropertyRelative("_rotationX").intValue =
                            variantObj["x"]?.Value<int>() ?? 0;
                        entry.FindPropertyRelative("_rotationY").intValue =
                            variantObj["y"]?.Value<int>() ?? 0;
                        entry.FindPropertyRelative("_uvlock").boolValue =
                            variantObj["uvlock"]?.Value<bool>() ?? false;
                        entry.FindPropertyRelative("_weight").intValue = 1;

                        // Wire model ref
                        string modelStr = variantObj["model"]?.Value<string>();

                        if (!string.IsNullOrEmpty(modelStr))
                        {
                            string modelName = ExtractModelName(modelStr);

                            if (_modelCache.TryGetValue(modelName, out BlockModel model))
                            {
                                entry.FindPropertyRelative("_model").objectReferenceValue = model;
                            }
                        }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/BlockStates/{blockName}.asset";
                AssetDatabase.CreateAsset(mapping, assetPath);
                _mappingCache[blockName] = mapping;
            }
        }

        private static void ConvertLootTables()
        {
            string primaryDir = Path.Combine(ContentRoot, "data", "loot_tables", "blocks");
            string altDir = Path.Combine(ContentRoot, "loot_tables", "blocks");

            List<string> allFiles = new List<string>();
            HashSet<string> seenNames = new HashSet<string>();

            // Primary path first
            if (Directory.Exists(primaryDir))
            {
                string[] primaryFiles = Directory.GetFiles(primaryDir, "*.json");

                for (int i = 0; i < primaryFiles.Length; i++)
                {
                    allFiles.Add(primaryFiles[i]);
                    seenNames.Add(Path.GetFileNameWithoutExtension(primaryFiles[i]));
                }
            }

            // Alt path: only add files not already found in primary
            if (Directory.Exists(altDir))
            {
                string[] altFiles = Directory.GetFiles(altDir, "*.json");

                for (int i = 0; i < altFiles.Length; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(altFiles[i]);

                    if (!seenNames.Contains(name))
                    {
                        allFiles.Add(altFiles[i]);
                        seenNames.Add(name);
                    }
                }
            }

            if (allFiles.Count == 0)
            {
                return;
            }

            string[] files = allFiles.ToArray();

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string tableName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                LootTable lootTable = ScriptableObject.CreateInstance<LootTable>();
                SerializedObject so = new SerializedObject(lootTable);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_tableName").stringValue = "blocks/" + tableName;
                so.FindProperty("_type").stringValue = root["type"]?.Value<string>() ?? "block";

                SerializedProperty poolsProp = so.FindProperty("_pools");
                JArray poolsArray = root["pools"] as JArray;

                if (poolsArray != null)
                {
                    for (int p = 0; p < poolsArray.Count; p++)
                    {
                        JObject poolObj = poolsArray[p] as JObject;

                        if (poolObj == null)
                        {
                            continue;
                        }

                        poolsProp.InsertArrayElementAtIndex(poolsProp.arraySize);
                        SerializedProperty poolEntry = poolsProp.GetArrayElementAtIndex(
                            poolsProp.arraySize - 1);

                        // Rolls
                        JToken rollsToken = poolObj["rolls"];

                        if (rollsToken != null && rollsToken.Type == JTokenType.Integer)
                        {
                            int rolls = rollsToken.Value<int>();
                            poolEntry.FindPropertyRelative("_rollsMin").intValue = rolls;
                            poolEntry.FindPropertyRelative("_rollsMax").intValue = rolls;
                        }
                        else if (rollsToken != null && rollsToken.Type == JTokenType.Object)
                        {
                            poolEntry.FindPropertyRelative("_rollsMin").intValue =
                                rollsToken["min"]?.Value<int>() ?? 1;
                            poolEntry.FindPropertyRelative("_rollsMax").intValue =
                                rollsToken["max"]?.Value<int>() ?? 1;
                        }

                        // Entries
                        SerializedProperty entriesProp = poolEntry.FindPropertyRelative("_entries");
                        JArray entriesArray = poolObj["entries"] as JArray;

                        if (entriesArray != null)
                        {
                            for (int e = 0; e < entriesArray.Count; e++)
                            {
                                JObject entryObj = entriesArray[e] as JObject;

                                if (entryObj == null)
                                {
                                    continue;
                                }

                                entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                                SerializedProperty entryEntry = entriesProp.GetArrayElementAtIndex(
                                    entriesProp.arraySize - 1);

                                entryEntry.FindPropertyRelative("_type").stringValue =
                                    entryObj["type"]?.Value<string>() ?? "item";
                                entryEntry.FindPropertyRelative("_itemName").stringValue =
                                    entryObj["name"]?.Value<string>() ?? "";
                                entryEntry.FindPropertyRelative("_weight").intValue =
                                    entryObj["weight"]?.Value<int>() ?? 1;
                            }
                        }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/LootTables/{tableName}.asset";
                AssetDatabase.CreateAsset(lootTable, assetPath);
                _lootCache[tableName] = lootTable;
            }
        }

        private static void ConvertBlocks()
        {
            string blocksDir = Path.Combine(ContentRoot, "data", "block");

            if (!Directory.Exists(blocksDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(blocksDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string blockName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                BlockDefinition block = ScriptableObject.CreateInstance<BlockDefinition>();
                SerializedObject so = new SerializedObject(block);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_blockName").stringValue = blockName;
                so.FindProperty("_hardness").doubleValue = root["hardness"]?.Value<double>() ?? 1.0;
                so.FindProperty("_blastResistance").doubleValue =
                    root["blast_resistance"]?.Value<double>() ?? 1.0;
                so.FindProperty("_requiresTool").boolValue =
                    root["requires_tool"]?.Value<bool>() ?? false;
                so.FindProperty("_soundGroup").stringValue =
                    root["sound_group"]?.Value<string>() ?? "stone";
                so.FindProperty("_lightEmission").intValue =
                    root["light_emission"]?.Value<int>() ?? 0;
                so.FindProperty("_lightFilter").intValue =
                    root["light_filter"]?.Value<int>() ?? 15;
                so.FindProperty("_mapColor").stringValue =
                    root["map_color"]?.Value<string>() ?? "#808080";

                // Collision shape
                string collisionStr = root["collision_shape"]?.Value<string>() ?? "full_cube";
                so.FindProperty("_collisionShape").enumValueIndex =
                    (int)ParseCollisionShape(collisionStr);

                // Render layer
                string renderStr = root["render_layer"]?.Value<string>() ?? "opaque";
                so.FindProperty("_renderLayer").enumValueIndex =
                    (int)ParseRenderLayer(renderStr);

                // Loot table reference
                string lootStr = root["loot_table"]?.Value<string>();

                if (!string.IsNullOrEmpty(lootStr))
                {
                    string lootName = ExtractSimpleName(lootStr);

                    if (_lootCache.TryGetValue(lootName, out LootTable loot))
                    {
                        so.FindProperty("_lootTable").objectReferenceValue = loot;
                    }
                }

                // Block state mapping reference
                if (_mappingCache.TryGetValue(blockName, out BlockStateMapping mapping))
                {
                    so.FindProperty("_blockStateMapping").objectReferenceValue = mapping;
                }

                // Properties
                JToken propsToken = root["properties"];

                if (propsToken is JObject propsObj)
                {
                    SerializedProperty propsProp = so.FindProperty("_properties");

                    foreach (KeyValuePair<string, JToken> prop in propsObj)
                    {
                        JObject propDef = prop.Value as JObject;

                        if (propDef == null)
                        {
                            continue;
                        }

                        propsProp.InsertArrayElementAtIndex(propsProp.arraySize);
                        SerializedProperty entry = propsProp.GetArrayElementAtIndex(
                            propsProp.arraySize - 1);

                        entry.FindPropertyRelative("_name").stringValue = prop.Key;

                        string type = propDef["type"]?.Value<string>() ?? "";
                        string defaultVal = propDef["default"]?.Value<string>() ?? "";

                        if (string.Equals(type, "bool", StringComparison.Ordinal))
                        {
                            entry.FindPropertyRelative("_kind").enumValueIndex =
                                (int)BlockPropertyKind.Bool;
                            entry.FindPropertyRelative("_defaultValue").stringValue = defaultVal;
                        }
                        else if (string.Equals(type, "int", StringComparison.Ordinal))
                        {
                            entry.FindPropertyRelative("_kind").enumValueIndex =
                                (int)BlockPropertyKind.IntRange;
                            entry.FindPropertyRelative("_minValue").intValue =
                                propDef["min"]?.Value<int>() ?? 0;
                            entry.FindPropertyRelative("_maxValue").intValue =
                                propDef["max"]?.Value<int>() ?? 0;
                            entry.FindPropertyRelative("_defaultValue").stringValue = defaultVal;
                        }
                        else if (string.Equals(type, "enum", StringComparison.Ordinal))
                        {
                            entry.FindPropertyRelative("_kind").enumValueIndex =
                                (int)BlockPropertyKind.Enum;
                            entry.FindPropertyRelative("_defaultValue").stringValue = defaultVal;

                            SerializedProperty valuesProp =
                                entry.FindPropertyRelative("_values");
                            JArray valuesArray = propDef["values"] as JArray;

                            if (valuesArray != null)
                            {
                                for (int v = 0; v < valuesArray.Count; v++)
                                {
                                    valuesProp.InsertArrayElementAtIndex(valuesProp.arraySize);
                                    valuesProp.GetArrayElementAtIndex(
                                        valuesProp.arraySize - 1).stringValue =
                                        valuesArray[v].Value<string>();
                                }
                            }
                        }
                    }
                }

                // Tags
                JArray tagsArray = root["tags"] as JArray;

                if (tagsArray != null)
                {
                    SerializedProperty tagsProp = so.FindProperty("_tags");

                    for (int t = 0; t < tagsArray.Count; t++)
                    {
                        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue =
                            tagsArray[t].Value<string>();
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Blocks/{blockName}.asset";
                AssetDatabase.CreateAsset(block, assetPath);
                _blockCache[blockName] = block;
            }
        }

        private static void ConvertItems()
        {
            string itemsDir = Path.Combine(ContentRoot, "data", "item");

            if (!Directory.Exists(itemsDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(itemsDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string itemName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SerializedObject so = new SerializedObject(item);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_itemName").stringValue = itemName;
                so.FindProperty("_maxStackSize").intValue =
                    root["max_stack_size"]?.Value<int>() ?? 64;
                so.FindProperty("_toolLevel").intValue =
                    root["tool_level"]?.Value<int>() ?? 0;
                so.FindProperty("_durability").intValue =
                    root["durability"]?.Value<int>() ?? 0;
                so.FindProperty("_attackDamage").floatValue =
                    root["attack_damage"]?.Value<float>() ?? 1.0f;
                so.FindProperty("_attackSpeed").floatValue =
                    root["attack_speed"]?.Value<float>() ?? 4.0f;
                so.FindProperty("_miningSpeed").floatValue =
                    root["mining_speed"]?.Value<float>() ?? 1.0f;

                // Tool type
                string toolTypeStr = root["tool_type"]?.Value<string>();

                if (!string.IsNullOrEmpty(toolTypeStr))
                {
                    so.FindProperty("_toolType").enumValueIndex =
                        (int)ParseToolType(toolTypeStr);
                }

                // Block placement ref
                string blockIdStr = root["block_id"]?.Value<string>();

                if (!string.IsNullOrEmpty(blockIdStr))
                {
                    string blockName = ExtractSimpleName(blockIdStr);

                    if (_blockCache.TryGetValue(blockName, out BlockDefinition blockDef))
                    {
                        so.FindProperty("_placesBlock").objectReferenceValue = blockDef;
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Items/{itemName}.asset";
                AssetDatabase.CreateAsset(item, assetPath);
                _itemCache[itemName] = item;
            }
        }

        private static void ConvertTags()
        {
            string tagsDir = Path.Combine(ContentRoot, "data", "tags");

            if (!Directory.Exists(tagsDir))
            {
                return;
            }

            ConvertTagsRecursive(tagsDir, "");
        }

        private static void ConvertTagsRecursive(string directory, string prefix)
        {
            string[] files = Directory.GetFiles(directory, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string tagFileName = Path.GetFileNameWithoutExtension(filePath);
                string tagName = string.IsNullOrEmpty(prefix)
                    ? tagFileName
                    : prefix + "/" + tagFileName;
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                Tag tag = ScriptableObject.CreateInstance<Tag>();
                SerializedObject so = new SerializedObject(tag);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_tagName").stringValue = tagName;
                so.FindProperty("_replace").boolValue =
                    root["replace"]?.Value<bool>() ?? false;

                JArray valuesArray = root["values"] as JArray;

                if (valuesArray != null)
                {
                    SerializedProperty entriesProp = so.FindProperty("_entries");
                    SerializedProperty entryIdsProp = so.FindProperty("_entryIds");

                    for (int v = 0; v < valuesArray.Count; v++)
                    {
                        string value = valuesArray[v].Value<string>();

                        if (string.IsNullOrEmpty(value))
                        {
                            continue;
                        }

                        entryIdsProp.InsertArrayElementAtIndex(entryIdsProp.arraySize);
                        entryIdsProp.GetArrayElementAtIndex(
                            entryIdsProp.arraySize - 1).stringValue = value;

                        // Try to wire SO ref
                        string simpleName = ExtractSimpleName(value);

                        if (_blockCache.TryGetValue(simpleName, out BlockDefinition blockDef))
                        {
                            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                            entriesProp.GetArrayElementAtIndex(
                                entriesProp.arraySize - 1).objectReferenceValue = blockDef;
                        }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string safeName = tagName.Replace("/", "_");
                string assetPath = $"{OutputRoot}/Tags/{safeName}.asset";
                AssetDatabase.CreateAsset(tag, assetPath);
            }

            string[] subDirs = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirs.Length; i++)
            {
                string subDirName = Path.GetFileName(subDirs[i]);
                string newPrefix = string.IsNullOrEmpty(prefix)
                    ? subDirName
                    : prefix + "/" + subDirName;
                ConvertTagsRecursive(subDirs[i], newPrefix);
            }
        }

        private static void ConvertRecipes()
        {
            string recipesDir = Path.Combine(ContentRoot, "data", "recipes");

            if (!Directory.Exists(recipesDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(recipesDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string recipeName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                SerializedObject so = new SerializedObject(recipe);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_recipeName").stringValue = recipeName;

                string typeStr = root["type"]?.Value<string>() ?? "shaped";
                so.FindProperty("_type").enumValueIndex =
                    string.Equals(typeStr, "shapeless", StringComparison.Ordinal) ? 1 : 0;

                // Result
                JObject resultObj = root["result"] as JObject;

                if (resultObj != null)
                {
                    string resultItemStr = resultObj["item"]?.Value<string>();
                    so.FindProperty("_resultItemId").stringValue = resultItemStr ?? "";
                    so.FindProperty("_resultCount").intValue =
                        resultObj["count"]?.Value<int>() ?? 1;
                }

                // Pattern
                JArray patternArray = root["pattern"] as JArray;

                if (patternArray != null)
                {
                    SerializedProperty patternProp = so.FindProperty("_pattern");

                    for (int p = 0; p < patternArray.Count; p++)
                    {
                        patternProp.InsertArrayElementAtIndex(patternProp.arraySize);
                        patternProp.GetArrayElementAtIndex(
                            patternProp.arraySize - 1).stringValue =
                            patternArray[p].Value<string>();
                    }
                }

                // Keys
                JObject keyObj = root["key"] as JObject;

                if (keyObj != null)
                {
                    SerializedProperty keysProp = so.FindProperty("_keys");

                    foreach (KeyValuePair<string, JToken> kvp in keyObj)
                    {
                        if (kvp.Key.Length == 0)
                        {
                            continue;
                        }

                        keysProp.InsertArrayElementAtIndex(keysProp.arraySize);
                        SerializedProperty entry = keysProp.GetArrayElementAtIndex(
                            keysProp.arraySize - 1);

                        entry.FindPropertyRelative("_key").intValue = kvp.Key[0];

                        string itemStr = null;

                        if (kvp.Value.Type == JTokenType.Object)
                        {
                            itemStr = kvp.Value["item"]?.Value<string>();
                        }
                        else if (kvp.Value.Type == JTokenType.String)
                        {
                            itemStr = kvp.Value.Value<string>();
                        }

                        entry.FindPropertyRelative("_itemId").stringValue = itemStr ?? "";
                    }
                }

                // Shapeless ingredients
                JArray ingredientsArray = root["ingredients"] as JArray;

                if (ingredientsArray != null)
                {
                    SerializedProperty ingredientsProp = so.FindProperty("_ingredients");

                    for (int ing = 0; ing < ingredientsArray.Count; ing++)
                    {
                        JToken ingToken = ingredientsArray[ing];
                        string itemStr = null;

                        if (ingToken.Type == JTokenType.Object)
                        {
                            itemStr = ingToken["item"]?.Value<string>();
                        }
                        else if (ingToken.Type == JTokenType.String)
                        {
                            itemStr = ingToken.Value<string>();
                        }

                        ingredientsProp.InsertArrayElementAtIndex(ingredientsProp.arraySize);
                        SerializedProperty entry = ingredientsProp.GetArrayElementAtIndex(
                            ingredientsProp.arraySize - 1);
                        entry.FindPropertyRelative("_itemId").stringValue = itemStr ?? "";
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Recipes/{recipeName}.asset";
                AssetDatabase.CreateAsset(recipe, assetPath);
            }
        }

        private static void ConvertBiomes()
        {
            string biomesDir = Path.Combine(ContentRoot, "data", "worldgen", "biome");

            if (!Directory.Exists(biomesDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(biomesDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string biomeName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                BiomeDefinition biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                SerializedObject so = new SerializedObject(biome);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_biomeName").stringValue = biomeName;
                so.FindProperty("_temperatureMin").floatValue =
                    root["temperature_min"]?.Value<float>() ?? 0f;
                so.FindProperty("_temperatureMax").floatValue =
                    root["temperature_max"]?.Value<float>() ?? 1f;
                so.FindProperty("_temperatureCenter").floatValue =
                    root["temperature_center"]?.Value<float>() ?? 0.5f;
                so.FindProperty("_humidityMin").floatValue =
                    root["humidity_min"]?.Value<float>() ?? 0f;
                so.FindProperty("_humidityMax").floatValue =
                    root["humidity_max"]?.Value<float>() ?? 1f;
                so.FindProperty("_humidityCenter").floatValue =
                    root["humidity_center"]?.Value<float>() ?? 0.5f;
                so.FindProperty("_fillerDepth").intValue =
                    root["filler_depth"]?.Value<int>() ?? 3;
                so.FindProperty("_treeDensity").floatValue =
                    root["tree_density"]?.Value<float>() ?? 0f;
                so.FindProperty("_heightModifier").floatValue =
                    root["height_modifier"]?.Value<float>() ?? 0f;

                // Wire block references
                WireBlockRef(so, "_topBlock", root["top_block"]?.Value<string>());
                WireBlockRef(so, "_fillerBlock", root["filler_block"]?.Value<string>());
                WireBlockRef(so, "_stoneBlock", root["stone_block"]?.Value<string>());
                WireBlockRef(so, "_underwaterBlock", root["underwater_block"]?.Value<string>());

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Biomes/{biomeName}.asset";
                AssetDatabase.CreateAsset(biome, assetPath);
            }
        }

        private static void ConvertOres()
        {
            string oresDir = Path.Combine(ContentRoot, "data", "worldgen", "ore");

            if (!Directory.Exists(oresDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(oresDir, "*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string oreName = Path.GetFileNameWithoutExtension(filePath);
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                OreDefinition ore = ScriptableObject.CreateInstance<OreDefinition>();
                SerializedObject so = new SerializedObject(ore);

                so.FindProperty("_namespace").stringValue = "lithforge";
                so.FindProperty("_oreName").stringValue = oreName;
                so.FindProperty("_minY").intValue = root["min_y"]?.Value<int>() ?? 0;
                so.FindProperty("_maxY").intValue = root["max_y"]?.Value<int>() ?? 128;
                so.FindProperty("_veinSize").intValue = root["vein_size"]?.Value<int>() ?? 8;
                so.FindProperty("_frequency").floatValue =
                    root["frequency"]?.Value<float>() ?? 1.0f;

                string oreTypeStr = root["ore_type"]?.Value<string>() ?? "blob";
                so.FindProperty("_oreType").enumValueIndex =
                    string.Equals(oreTypeStr, "scatter", StringComparison.Ordinal) ? 1 : 0;

                WireBlockRef(so, "_oreBlock", root["ore_block"]?.Value<string>());
                WireBlockRef(so, "_replaceBlock", root["replace_block"]?.Value<string>());

                so.ApplyModifiedPropertiesWithoutUndo();

                string assetPath = $"{OutputRoot}/Ores/{oreName}.asset";
                AssetDatabase.CreateAsset(ore, assetPath);
            }
        }

        private static void WireBlockReferences()
        {
            // Wire loot table references on blocks that reference by string path
            // already done inline during ConvertBlocks
        }

        private static void WireBlockRef(SerializedObject so, string propertyName, string blockIdStr)
        {
            if (string.IsNullOrEmpty(blockIdStr))
            {
                return;
            }

            string blockName = ExtractSimpleName(blockIdStr);

            if (_blockCache.TryGetValue(blockName, out BlockDefinition blockDef))
            {
                so.FindProperty(propertyName).objectReferenceValue = blockDef;
            }
        }

        private static bool IsBuiltInModel(string parentRef)
        {
            if (string.IsNullOrEmpty(parentRef))
            {
                return false;
            }

            return parentRef.Equals("lithforge:block/cube") ||
                   parentRef.Equals("lithforge:block/cube_all") ||
                   parentRef.Equals("lithforge:block/cube_column") ||
                   parentRef.Equals("lithforge:block/cube_bottom_top") ||
                   parentRef.Equals("lithforge:block/orientable") ||
                   parentRef.Equals("lithforge:block/cross") ||
                   parentRef.Equals("lithforge:item/generated") ||
                   parentRef.Equals("lithforge:item/handheld");
        }

        private static BuiltInParentType ParseBuiltInParent(string parentRef)
        {
            if (parentRef.Equals("lithforge:block/cube_all"))
            {
                return BuiltInParentType.CubeAll;
            }

            if (parentRef.Equals("lithforge:block/cube_column"))
            {
                return BuiltInParentType.CubeColumn;
            }

            if (parentRef.Equals("lithforge:block/cube"))
            {
                return BuiltInParentType.Cube;
            }

            if (parentRef.Equals("lithforge:block/cube_bottom_top"))
            {
                return BuiltInParentType.CubeBottomTop;
            }

            if (parentRef.Equals("lithforge:block/orientable"))
            {
                return BuiltInParentType.Orientable;
            }

            if (parentRef.Equals("lithforge:block/cross") ||
                parentRef.Equals("lithforge:item/generated") ||
                parentRef.Equals("lithforge:item/handheld"))
            {
                return BuiltInParentType.Cross;
            }

            return BuiltInParentType.None;
        }

        private static string ExtractModelName(string resourceId)
        {
            // "lithforge:block/stone" → "stone"
            int colonIndex = resourceId.IndexOf(':');

            if (colonIndex >= 0)
            {
                string path = resourceId.Substring(colonIndex + 1);
                int slashIndex = path.LastIndexOf('/');

                if (slashIndex >= 0)
                {
                    return path.Substring(slashIndex + 1);
                }

                return path;
            }

            return resourceId;
        }

        private static string ExtractSimpleName(string resourceId)
        {
            // "lithforge:stone" → "stone" or "lithforge:blocks/stone" → "stone"
            int colonIndex = resourceId.IndexOf(':');

            if (colonIndex >= 0)
            {
                string path = resourceId.Substring(colonIndex + 1);
                int slashIndex = path.LastIndexOf('/');

                if (slashIndex >= 0)
                {
                    return path.Substring(slashIndex + 1);
                }

                return path;
            }

            return resourceId;
        }

        private static CollisionShapeType ParseCollisionShape(string value)
        {
            switch (value)
            {
                case "none":
                    return CollisionShapeType.None;
                case "slab":
                    return CollisionShapeType.Slab;
                case "stairs":
                    return CollisionShapeType.Stairs;
                case "fence":
                    return CollisionShapeType.Fence;
                default:
                    return CollisionShapeType.FullCube;
            }
        }

        private static RenderLayerType ParseRenderLayer(string value)
        {
            switch (value)
            {
                case "cutout":
                    return RenderLayerType.Cutout;
                case "translucent":
                    return RenderLayerType.Translucent;
                default:
                    return RenderLayerType.Opaque;
            }
        }

        private static ToolType ParseToolType(string value)
        {
            switch (value)
            {
                case "pickaxe":
                    return ToolType.Pickaxe;
                case "axe":
                    return ToolType.Axe;
                case "shovel":
                    return ToolType.Shovel;
                case "hoe":
                    return ToolType.Hoe;
                case "sword":
                    return ToolType.Sword;
                default:
                    return ToolType.None;
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folderName = Path.GetFileName(path);

                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }

                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
