using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
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
        public BiomeDefinition[] BiomeDefinitions { get; }
        public OreDefinition[] OreDefinitions { get; }
        public List<ItemEntry> ItemEntries { get; }
        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; }
        public TagRegistry TagRegistry { get; }
        public ItemRegistry ItemRegistry { get; }
        public CraftingEngine CraftingEngine { get; }
        public ItemSpriteAtlas ItemSpriteAtlas { get; }
        public BlockEntityRegistry BlockEntityRegistry { get; }
        public SmeltingRecipeRegistry SmeltingRecipeRegistry { get; }
        public ItemDisplayTransformLookup DisplayTransformLookup { get; }
        public ToolMaterialRegistry ToolMaterialRegistry { get; }
        public ToolTraitRegistry ToolTraitRegistry { get; }

        /// <summary>
        /// Pre-baked serialized ToolInstance data for legacy tool items.
        /// Keyed by item ResourceId. Used to populate CustomData when
        /// these items are created at runtime (crafting, commands, etc.).
        /// </summary>
        public Dictionary<ResourceId, byte[]> LegacyToolTemplates { get; }

        public ContentPipelineResult(
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            AtlasResult atlasResult,
            BiomeDefinition[] biomeDefinitions,
            OreDefinition[] oreDefinitions,
            List<ItemEntry> itemEntries,
            Dictionary<ResourceId, LootTableDefinition> lootTables,
            TagRegistry tagRegistry,
            ItemRegistry itemRegistry,
            CraftingEngine craftingEngine,
            ItemSpriteAtlas itemSpriteAtlas,
            BlockEntityRegistry blockEntityRegistry,
            SmeltingRecipeRegistry smeltingRecipeRegistry,
            ItemDisplayTransformLookup displayTransformLookup,
            ToolMaterialRegistry toolMaterialRegistry,
            ToolTraitRegistry toolTraitRegistry,
            Dictionary<ResourceId, byte[]> legacyToolTemplates)
        {
            StateRegistry = stateRegistry;
            NativeStateRegistry = nativeStateRegistry;
            NativeAtlasLookup = nativeAtlasLookup;
            AtlasResult = atlasResult;
            BiomeDefinitions = biomeDefinitions;
            OreDefinitions = oreDefinitions;
            ItemEntries = itemEntries;
            LootTables = lootTables;
            TagRegistry = tagRegistry;
            ItemRegistry = itemRegistry;
            CraftingEngine = craftingEngine;
            ItemSpriteAtlas = itemSpriteAtlas;
            BlockEntityRegistry = blockEntityRegistry;
            SmeltingRecipeRegistry = smeltingRecipeRegistry;
            DisplayTransformLookup = displayTransformLookup;
            ToolMaterialRegistry = toolMaterialRegistry;
            ToolTraitRegistry = toolTraitRegistry;
            LegacyToolTemplates = legacyToolTemplates;
        }
    }
}
