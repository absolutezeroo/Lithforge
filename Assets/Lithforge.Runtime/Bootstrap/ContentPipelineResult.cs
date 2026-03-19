using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Item.Crafting;
using Lithforge.Item;
using Lithforge.Item.Loot;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Tag;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Result of the content pipeline build.
    ///     Contains all registries and atlas data needed for runtime operation.
    /// </summary>
    public sealed class ContentPipelineResult
    {
        /// <summary>Creates a content pipeline result with all registries and data from the build.</summary>
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
            SoundGroupRegistry soundGroupRegistry,
            ToolPartTextureDatabase toolPartTextures,
            ToolMaterialDefinition[] toolMaterials,
            ToolTemplateRegistry toolTemplateRegistry,
            PartBuilderRecipeRegistry partBuilderRecipeRegistry,
            MaterialInputRegistry materialInputRegistry)
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
            SoundGroupRegistry = soundGroupRegistry;
            ToolPartTextures = toolPartTextures;
            ToolMaterials = toolMaterials;
            ToolTemplateRegistry = toolTemplateRegistry;
            PartBuilderRecipeRegistry = partBuilderRecipeRegistry;
            MaterialInputRegistry = materialInputRegistry;
        }
        /// <summary>Managed state registry with all block states and their properties.</summary>
        public StateRegistry StateRegistry { get; }

        /// <summary>Burst-accessible native state registry with BlockStateCompact data.</summary>
        public NativeStateRegistry NativeStateRegistry { get; }

        /// <summary>Burst-accessible native atlas texture lookup.</summary>
        public NativeAtlasLookup NativeAtlasLookup { get; }

        /// <summary>Built Texture2DArray atlas and index mappings.</summary>
        public AtlasResult AtlasResult { get; }

        /// <summary>All loaded biome definitions.</summary>
        public BiomeDefinition[] BiomeDefinitions { get; }

        /// <summary>All loaded ore definitions.</summary>
        public OreDefinition[] OreDefinitions { get; }

        /// <summary>All built item entries from blocks and standalone items.</summary>
        public List<ItemEntry> ItemEntries { get; }

        /// <summary>Loot tables keyed by ResourceId for block drop resolution.</summary>
        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; }

        /// <summary>Tag registry with bidirectional tag-to-block lookup.</summary>
        public TagRegistry TagRegistry { get; }

        /// <summary>Item registry for lookup by ResourceId.</summary>
        public ItemRegistry ItemRegistry { get; }

        /// <summary>Crafting engine with all loaded shaped and shapeless recipes.</summary>
        public CraftingEngine CraftingEngine { get; }

        /// <summary>Atlas of item sprites for UI rendering.</summary>
        public ItemSpriteAtlas ItemSpriteAtlas { get; }

        /// <summary>Registry of block entity types and factories.</summary>
        public BlockEntityRegistry BlockEntityRegistry { get; }

        /// <summary>Registry of smelting recipes for furnace processing.</summary>
        public SmeltingRecipeRegistry SmeltingRecipeRegistry { get; }

        /// <summary>Lookup for item display transforms (rotation, scale, offset).</summary>
        public ItemDisplayTransformLookup DisplayTransformLookup { get; }

        /// <summary>Registry of tool materials for part crafting.</summary>
        public ToolMaterialRegistry ToolMaterialRegistry { get; }

        /// <summary>Registry of tool traits for modifier application.</summary>
        public ToolTraitRegistry ToolTraitRegistry { get; }

        /// <summary>Registry of sound groups for block material sounds.</summary>
        public SoundGroupRegistry SoundGroupRegistry { get; }

        /// <summary>Database of tool part textures for composite tool rendering.</summary>
        public ToolPartTextureDatabase ToolPartTextures { get; }

        /// <summary>All loaded tool material definitions.</summary>
        public ToolMaterialDefinition[] ToolMaterials { get; }

        /// <summary>Registry of pre-built tool templates for starting items.</summary>
        public ToolTemplateRegistry ToolTemplateRegistry { get; }

        /// <summary>Registry of part builder recipes for tool construction.</summary>
        public PartBuilderRecipeRegistry PartBuilderRecipeRegistry { get; }

        /// <summary>Registry mapping material items to tool materials.</summary>
        public MaterialInputRegistry MaterialInputRegistry { get; }
    }
}
