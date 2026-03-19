using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Core.Validation;
using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Item.Loot;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Tag;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Shared mutable state passed through all content phases.
    ///     Each phase reads its inputs from and writes its outputs to this context.
    /// </summary>
    public sealed class ContentPhaseContext
    {
        /// <summary>Logger for pipeline diagnostics and warnings.</summary>
        public ILogger Logger { get; set; }

        /// <summary>Content validator for checking definition integrity.</summary>
        public ContentValidator Validator { get; set; }

        /// <summary>Tile size in pixels for atlas texture entries.</summary>
        public int AtlasTileSize { get; set; } = 16;

        /// <summary>All loaded BlockDefinition ScriptableObjects.</summary>
        public BlockDefinition[] BlockDefinitions { get; set; }

        /// <summary>Mutable state registry being built during the pipeline.</summary>
        public StateRegistry StateRegistry { get; set; }

        /// <summary>Lookup from "namespace:name" to BlockDefinition for variant resolution.</summary>
        public Dictionary<string, BlockDefinition> BlockLookup { get; set; }

        /// <summary>Content model resolver for parent inheritance chain resolution.</summary>
        public ContentModelResolver ModelResolver { get; set; }

        /// <summary>Per-state resolved face textures with tint and overlay data.</summary>
        public Dictionary<StateId, ResolvedFaceTextures2D> ResolvedFaces { get; set; }

        /// <summary>Built Texture2DArray atlas result.</summary>
        public AtlasResult AtlasResult { get; set; }

        /// <summary>All loaded biome definitions.</summary>
        public BiomeDefinition[] BiomeDefinitions { get; set; }

        /// <summary>All loaded ore definitions.</summary>
        public OreDefinition[] OreDefinitions { get; set; }

        /// <summary>All loaded item definitions.</summary>
        public ItemDefinition[] Items { get; set; }

        /// <summary>Built item entries from blocks and standalone items.</summary>
        public List<ItemEntry> ItemEntries { get; set; }

        /// <summary>Built item registry for lookup by ResourceId.</summary>
        public ItemRegistry ItemRegistry { get; set; }

        /// <summary>Crafting engine with all loaded recipes.</summary>
        public CraftingEngine CraftingEngine { get; set; }

        /// <summary>Loaded loot tables keyed by ResourceId.</summary>
        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; set; }

        /// <summary>Tag registry with bidirectional tag-to-block lookup.</summary>
        public TagRegistry TagRegistry { get; set; }

        /// <summary>All loaded tool material definitions.</summary>
        public ToolMaterialDefinition[] ToolMaterials { get; set; }

        /// <summary>Registry of tool materials for part crafting.</summary>
        public ToolMaterialRegistry ToolMaterialRegistry { get; set; }

        /// <summary>Registry mapping material items to tool materials.</summary>
        public MaterialInputRegistry MaterialInputRegistry { get; set; }

        /// <summary>All loaded tool definitions.</summary>
        public ToolDefinition[] ToolDefinitions { get; set; }

        /// <summary>Registry of tool traits for modifier application.</summary>
        public ToolTraitRegistry ToolTraitRegistry { get; set; }

        /// <summary>Registry of part builder recipes for tool construction.</summary>
        public PartBuilderRecipeRegistry PartBuilderRecipeRegistry { get; set; }

        /// <summary>Registry of smelting recipes for furnace processing.</summary>
        public SmeltingRecipeRegistry SmeltingRecipeRegistry { get; set; }

        /// <summary>Registry of block entity types and factories.</summary>
        public BlockEntityRegistry BlockEntityRegistry { get; set; }

        /// <summary>Registry of sound groups for block material sounds.</summary>
        public SoundGroupRegistry SoundGroupRegistry { get; set; }

        /// <summary>Registry of pre-built tool templates for starting items.</summary>
        public ToolTemplateRegistry ToolTemplateRegistry { get; set; }

        /// <summary>Burst-accessible native state registry built from bake.</summary>
        public NativeStateRegistry NativeStateRegistry { get; set; }

        /// <summary>Burst-accessible native atlas lookup built from bake.</summary>
        public NativeAtlasLookup NativeAtlasLookup { get; set; }

        /// <summary>Database of tool part textures for composite tool rendering.</summary>
        public ToolPartTextureDatabase ToolPartTextures { get; set; }

        /// <summary>Atlas of item sprites for UI rendering.</summary>
        public ItemSpriteAtlas ItemSpriteAtlas { get; set; }

        /// <summary>Lookup for item display transforms (rotation, scale, offset).</summary>
        public ItemDisplayTransformLookup DisplayTransformLookup { get; set; }
    }
}
