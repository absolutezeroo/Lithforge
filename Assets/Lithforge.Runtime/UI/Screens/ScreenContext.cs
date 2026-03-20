using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Item.Crafting;
using Lithforge.Item;
using Lithforge.Voxel.Crafting;

using UnityEngine.UIElements;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Immutable parameter object carrying all shared dependencies for container screens.
    /// Created once at bootstrap and passed to every screen's <c>Initialize(ScreenContext)</c>.
    /// Screens pull only what they need; unused fields are simply ignored.
    /// </summary>
    public sealed class ScreenContext
    {
        /// <summary>The player's inventory with 36 item slots.</summary>
        public Inventory PlayerInventory { get; }

        /// <summary>Registry mapping item ResourceIds to their definitions.</summary>
        public ItemRegistry ItemRegistry { get; }

        /// <summary>Atlas providing item sprites for UI display.</summary>
        public ItemSpriteAtlas ItemSpriteAtlas { get; }

        /// <summary>Shared panel settings for UI Toolkit documents.</summary>
        public PanelSettings PanelSettings { get; }

        /// <summary>
        /// May be null for screens that do not use crafting.
        /// </summary>
        public CraftingEngine CraftingEngine { get; }

        /// <summary>
        /// May be null for screens that do not use tool assembly.
        /// </summary>
        public ToolTraitRegistry ToolTraitRegistry { get; }

        /// <summary>
        /// May be null if no ToolDefinition assets are loaded.
        /// </summary>
        public ToolPartTextureDatabase ToolPartTextures { get; }

        /// <summary>
        /// May be null if no ToolMaterialDefinition assets are loaded.
        /// </summary>
        public ToolMaterialDefinition[] ToolMaterials { get; }

        /// <summary>
        /// May be null if no legacy tool templates are loaded.
        /// </summary>
        public ToolTemplateRegistry ToolTemplateRegistry { get; }

        /// <summary>
        /// Part Builder recipe registry. May be null if no Part Builder recipes are loaded.
        /// </summary>
        public PartBuilderRecipeRegistry PartBuilderRecipeRegistry { get; }

        /// <summary>
        /// Tool material registry with craftable material lookup.
        /// May be null if no tool materials are loaded.
        /// </summary>
        public ToolMaterialRegistry ToolMaterialRegistry { get; }

        /// <summary>
        /// Material input registry mapping items to materials with value/needed ratios.
        /// Used by the Part Builder for TiC-style fractional material costs.
        /// May be null if no material inputs are configured.
        /// </summary>
        public MaterialInputRegistry MaterialInputRegistry { get; }

        /// <summary>
        /// May be null for screens that do not need cross-screen coordination.
        /// </summary>
        public ContainerScreenManager ScreenManager { get; }

        /// <summary>Logger for container screen diagnostics. May be null.</summary>
        public ILogger Logger { get; }

        /// <summary>Creates a ScreenContext with all shared dependencies for container screens.</summary>
        public ScreenContext(
            Inventory playerInventory,
            ItemRegistry itemRegistry,
            ItemSpriteAtlas itemSpriteAtlas,
            PanelSettings panelSettings,
            CraftingEngine craftingEngine,
            ToolTraitRegistry toolTraitRegistry,
            ToolPartTextureDatabase toolPartTextures,
            ToolMaterialDefinition[] toolMaterials,
            ToolTemplateRegistry toolTemplateRegistry,
            PartBuilderRecipeRegistry partBuilderRecipeRegistry,
            ToolMaterialRegistry toolMaterialRegistry,
            MaterialInputRegistry materialInputRegistry,
            ContainerScreenManager screenManager = null,
            ILogger logger = null)
        {
            PlayerInventory = playerInventory;
            ItemRegistry = itemRegistry;
            ItemSpriteAtlas = itemSpriteAtlas;
            PanelSettings = panelSettings;
            CraftingEngine = craftingEngine;
            ToolTraitRegistry = toolTraitRegistry;
            ToolPartTextures = toolPartTextures;
            ToolMaterials = toolMaterials;
            ToolTemplateRegistry = toolTemplateRegistry;
            PartBuilderRecipeRegistry = partBuilderRecipeRegistry;
            ToolMaterialRegistry = toolMaterialRegistry;
            MaterialInputRegistry = materialInputRegistry;
            ScreenManager = screenManager;
            Logger = logger;
        }
    }
}
