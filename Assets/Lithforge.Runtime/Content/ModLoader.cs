using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    /// <summary>
    /// Loads .lithmod files (AssetBundles) from Application.persistentDataPath/mods/
    /// and feeds their content into the same registries as core content.
    /// </summary>
    public sealed class ModLoader
    {
        private readonly List<AssetBundle> _loadedBundles = new List<AssetBundle>();

        public List<BlockDefinition> LoadedBlocks { get; private set; }
        public List<BlockStateMapping> LoadedMappings { get; private set; }
        public List<BlockModel> LoadedModels { get; private set; }
        public List<ItemDefinition> LoadedItems { get; private set; }
        public List<RecipeDefinition> LoadedRecipes { get; private set; }
        public List<Tag> LoadedTags { get; private set; }
        public List<LootTable> LoadedLootTables { get; private set; }
        public List<BiomeDefinition> LoadedBiomes { get; private set; }
        public List<OreDefinition> LoadedOres { get; private set; }
        public List<ModManifest> LoadedManifests { get; private set; }

        public ModLoader()
        {
            LoadedBlocks = new List<BlockDefinition>();
            LoadedMappings = new List<BlockStateMapping>();
            LoadedModels = new List<BlockModel>();
            LoadedItems = new List<ItemDefinition>();
            LoadedRecipes = new List<RecipeDefinition>();
            LoadedTags = new List<Tag>();
            LoadedLootTables = new List<LootTable>();
            LoadedBiomes = new List<BiomeDefinition>();
            LoadedOres = new List<OreDefinition>();
            LoadedManifests = new List<ModManifest>();
        }

        public void LoadAllMods()
        {
            string modsDir = Path.Combine(Application.persistentDataPath, "mods");

            if (!Directory.Exists(modsDir))
            {
                Debug.Log("[ModLoader] No mods directory found.");
                return;
            }

            string[] modFiles = Directory.GetFiles(modsDir, "*.lithmod");

            for (int i = 0; i < modFiles.Length; i++)
            {
                LoadMod(modFiles[i]);
            }

            Debug.Log($"[ModLoader] Loaded {_loadedBundles.Count} mods: " +
                $"{LoadedBlocks.Count} blocks, {LoadedItems.Count} items, " +
                $"{LoadedBiomes.Count} biomes, {LoadedOres.Count} ores.");
        }

        private void LoadMod(string bundlePath)
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle == null)
            {
                Debug.LogError($"[ModLoader] Failed to load mod bundle: {bundlePath}");
                return;
            }

            _loadedBundles.Add(bundle);

            string modName = Path.GetFileNameWithoutExtension(bundlePath);
            Debug.Log($"[ModLoader] Loading mod: {modName}");

            LoadAssetsOfType(bundle, LoadedManifests);
            LoadAssetsOfType(bundle, LoadedBlocks);
            LoadAssetsOfType(bundle, LoadedMappings);
            LoadAssetsOfType(bundle, LoadedModels);
            LoadAssetsOfType(bundle, LoadedItems);
            LoadAssetsOfType(bundle, LoadedRecipes);
            LoadAssetsOfType(bundle, LoadedTags);
            LoadAssetsOfType(bundle, LoadedLootTables);
            LoadAssetsOfType(bundle, LoadedBiomes);
            LoadAssetsOfType(bundle, LoadedOres);
        }

        private static void LoadAssetsOfType<T>(AssetBundle bundle, List<T> target) where T : Object
        {
            T[] assets = bundle.LoadAllAssets<T>();

            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    target.Add(assets[i]);
                }
            }
        }

        public void UnloadAll()
        {
            for (int i = 0; i < _loadedBundles.Count; i++)
            {
                _loadedBundles[i].Unload(true);
            }

            _loadedBundles.Clear();
        }
    }
}
