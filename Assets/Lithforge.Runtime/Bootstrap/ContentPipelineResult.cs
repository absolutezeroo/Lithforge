using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Lithforge.Voxel.Tag;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Result of the content pipeline build.
    /// Contains all registries and atlas data needed for runtime operation.
    /// </summary>
    public sealed class ContentPipelineResult
    {
        public StateRegistry StateRegistry { get; }
        public NativeStateRegistry NativeStateRegistry { get; }
        public NativeAtlasLookup NativeAtlasLookup { get; }
        public AtlasResult AtlasResult { get; }
        public BiomeDefinitionSO[] BiomeDefinitions { get; }
        public OreDefinitionSO[] OreDefinitions { get; }
        public List<ItemDefinition> ItemDefinitions { get; }
        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; }
        public TagRegistry TagRegistry { get; }
        public ItemRegistry ItemRegistry { get; }
        public CraftingEngine CraftingEngine { get; }

        public ContentPipelineResult(
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            AtlasResult atlasResult,
            BiomeDefinitionSO[] biomeDefinitions,
            OreDefinitionSO[] oreDefinitions,
            List<ItemDefinition> itemDefinitions,
            Dictionary<ResourceId, LootTableDefinition> lootTables,
            TagRegistry tagRegistry,
            ItemRegistry itemRegistry,
            CraftingEngine craftingEngine)
        {
            StateRegistry = stateRegistry;
            NativeStateRegistry = nativeStateRegistry;
            NativeAtlasLookup = nativeAtlasLookup;
            AtlasResult = atlasResult;
            BiomeDefinitions = biomeDefinitions;
            OreDefinitions = oreDefinitions;
            ItemDefinitions = itemDefinitions;
            LootTables = lootTables;
            TagRegistry = tagRegistry;
            ItemRegistry = itemRegistry;
            CraftingEngine = craftingEngine;
        }
    }
}
