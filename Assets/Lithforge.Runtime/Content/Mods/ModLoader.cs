using System.Collections.Generic;
using System.IO;

using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items;
using Lithforge.Runtime.Content.Loot;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Recipes;
using Lithforge.Runtime.Content.Tags;
using Lithforge.Runtime.Content.WorldGen;

using UnityEngine;

namespace Lithforge.Runtime.Content.Mods
{
    /// <summary>
    ///     Loads .lithmod files (AssetBundles) from Application.persistentDataPath/mods/
    ///     and feeds their content into the same registries as core content.
    /// </summary>
    public sealed class ModLoader
    {
        /// <summary>
        ///     Keeps loaded AssetBundles alive so their assets stay valid until UnloadAll.
        /// </summary>
        private readonly List<AssetBundle> _loadedBundles = new();

        /// <summary>Block definitions extracted from all loaded mods, ready for StateRegistry.</summary>
        public List<BlockDefinition> LoadedBlocks { get; } = new();

        /// <summary>Block-state property mappings from mods, merged into the cartesian product expansion.</summary>
        public List<BlockStateMapping> LoadedMappings { get; } = new();

        /// <summary>Block models from mods, fed into ContentModelResolver for parent chain resolution.</summary>
        public List<BlockModel> LoadedModels { get; } = new();

        /// <summary>Item definitions from mods, registered into ItemRegistry alongside core items.</summary>
        public List<ItemDefinition> LoadedItems { get; } = new();

        /// <summary>Crafting recipes from mods, added to CraftingEngine during content build.</summary>
        public List<RecipeDefinition> LoadedRecipes { get; } = new();

        /// <summary>Tags from mods (e.g. mineable groups), merged into TagRegistry.</summary>
        public List<Tag> LoadedTags { get; } = new();

        /// <summary>Loot tables from mods, used by LootResolver for block/entity drops.</summary>
        public List<LootTable> LoadedLootTables { get; } = new();

        /// <summary>Biome definitions from mods, registered for world generation.</summary>
        public List<BiomeDefinition> LoadedBiomes { get; } = new();

        /// <summary>Ore definitions from mods, registered for ore generation jobs.</summary>
        public List<OreDefinition> LoadedOres { get; } = new();

        /// <summary>Parsed manifests carrying mod metadata (name, version, dependencies).</summary>
        public List<ModManifest> LoadedManifests { get; } = new();

        /// <summary>
        ///     Scans the mods directory for .lithmod files, loads each as an AssetBundle,
        ///     and extracts all recognized ScriptableObject types into the LoadedXxx lists.
        ///     Safe to call when no mods directory exists (logs and returns).
        /// </summary>
        public void LoadAllMods()
        {
            string modsDir = Path.Combine(Application.persistentDataPath, "mods");

            if (!Directory.Exists(modsDir))
            {
                UnityEngine.Debug.Log("[ModLoader] No mods directory found.");
                return;
            }

            string[] modFiles = Directory.GetFiles(modsDir, "*.lithmod");

            for (int i = 0; i < modFiles.Length; i++)
            {
                LoadMod(modFiles[i]);
            }

            UnityEngine.Debug.Log($"[ModLoader] Loaded {_loadedBundles.Count} mods: " +
                                  $"{LoadedBlocks.Count} blocks, {LoadedItems.Count} items, " +
                                  $"{LoadedBiomes.Count} biomes, {LoadedOres.Count} ores.");
        }

        /// <summary>
        ///     Loads a single .lithmod AssetBundle and appends its content to the LoadedXxx lists.
        ///     Logs an error and returns if the bundle fails to open.
        /// </summary>
        private void LoadMod(string bundlePath)
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle == null)
            {
                UnityEngine.Debug.LogError($"[ModLoader] Failed to load mod bundle: {bundlePath}");
                return;
            }

            _loadedBundles.Add(bundle);

            string modName = Path.GetFileNameWithoutExtension(bundlePath);
            UnityEngine.Debug.Log($"[ModLoader] Loading mod: {modName}");

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

        /// <summary>
        ///     Loads all assets of type T from the bundle and appends them to the target list.
        /// </summary>
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

        /// <summary>
        ///     Unloads every loaded AssetBundle and all assets they provided.
        ///     After this call all LoadedXxx lists are stale and should not be read.
        /// </summary>
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
