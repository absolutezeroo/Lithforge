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

        public List<BlockDefinitionSO> LoadedBlocks { get; private set; }
        public List<BlockStateMappingSO> LoadedMappings { get; private set; }
        public List<BlockModelSO> LoadedModels { get; private set; }
        public List<ItemDefinitionSO> LoadedItems { get; private set; }
        public List<RecipeDefinitionSO> LoadedRecipes { get; private set; }
        public List<TagSO> LoadedTags { get; private set; }
        public List<LootTableSO> LoadedLootTables { get; private set; }
        public List<BiomeDefinitionSO> LoadedBiomes { get; private set; }
        public List<OreDefinitionSO> LoadedOres { get; private set; }
        public List<ModManifestSO> LoadedManifests { get; private set; }

        public ModLoader()
        {
            LoadedBlocks = new List<BlockDefinitionSO>();
            LoadedMappings = new List<BlockStateMappingSO>();
            LoadedModels = new List<BlockModelSO>();
            LoadedItems = new List<ItemDefinitionSO>();
            LoadedRecipes = new List<RecipeDefinitionSO>();
            LoadedTags = new List<TagSO>();
            LoadedLootTables = new List<LootTableSO>();
            LoadedBiomes = new List<BiomeDefinitionSO>();
            LoadedOres = new List<OreDefinitionSO>();
            LoadedManifests = new List<ModManifestSO>();
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
