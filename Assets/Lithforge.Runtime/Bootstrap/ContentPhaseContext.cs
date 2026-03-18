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
        public ILogger Logger { get; set; }

        public ContentValidator Validator { get; set; }

        public int AtlasTileSize { get; set; } = 16;

        public BlockDefinition[] BlockDefinitions { get; set; }

        public StateRegistry StateRegistry { get; set; }

        public Dictionary<string, BlockDefinition> BlockLookup { get; set; }

        public ContentModelResolver ModelResolver { get; set; }

        public Dictionary<StateId, ResolvedFaceTextures2D> ResolvedFaces { get; set; }

        public AtlasResult AtlasResult { get; set; }

        public BiomeDefinition[] BiomeDefinitions { get; set; }

        public OreDefinition[] OreDefinitions { get; set; }

        public ItemDefinition[] Items { get; set; }

        public List<ItemEntry> ItemEntries { get; set; }

        public ItemRegistry ItemRegistry { get; set; }

        public CraftingEngine CraftingEngine { get; set; }

        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; set; }

        public TagRegistry TagRegistry { get; set; }

        public ToolMaterialDefinition[] ToolMaterials { get; set; }

        public ToolMaterialRegistry ToolMaterialRegistry { get; set; }

        public MaterialInputRegistry MaterialInputRegistry { get; set; }

        public ToolDefinition[] ToolDefinitions { get; set; }

        public ToolTraitRegistry ToolTraitRegistry { get; set; }

        public PartBuilderRecipeRegistry PartBuilderRecipeRegistry { get; set; }

        public SmeltingRecipeRegistry SmeltingRecipeRegistry { get; set; }

        public BlockEntityRegistry BlockEntityRegistry { get; set; }

        public SoundGroupRegistry SoundGroupRegistry { get; set; }

        public ToolTemplateRegistry ToolTemplateRegistry { get; set; }

        public NativeStateRegistry NativeStateRegistry { get; set; }

        public NativeAtlasLookup NativeAtlasLookup { get; set; }

        public ToolPartTextureDatabase ToolPartTextures { get; set; }

        public ItemSpriteAtlas ItemSpriteAtlas { get; set; }

        public ItemDisplayTransformLookup DisplayTransformLookup { get; set; }
    }
}
