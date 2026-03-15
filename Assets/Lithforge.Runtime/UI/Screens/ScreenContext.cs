using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Immutable parameter object carrying all shared dependencies for container screens.
    /// Created once at bootstrap and passed to every screen's <c>Initialize(ScreenContext)</c>.
    /// Screens pull only what they need; unused fields are simply ignored.
    /// </summary>
    public sealed class ScreenContext
    {
        private readonly Inventory _playerInventory;
        private readonly ItemRegistry _itemRegistry;
        private readonly ItemSpriteAtlas _itemSpriteAtlas;
        private readonly PanelSettings _panelSettings;
        private readonly CraftingEngine _craftingEngine;
        private readonly ToolTraitRegistry _toolTraitRegistry;

        public Inventory PlayerInventory
        {
            get { return _playerInventory; }
        }

        public ItemRegistry ItemRegistry
        {
            get { return _itemRegistry; }
        }

        public ItemSpriteAtlas ItemSpriteAtlas
        {
            get { return _itemSpriteAtlas; }
        }

        public PanelSettings PanelSettings
        {
            get { return _panelSettings; }
        }

        /// <summary>
        /// May be null for screens that do not use crafting.
        /// </summary>
        public CraftingEngine CraftingEngine
        {
            get { return _craftingEngine; }
        }

        /// <summary>
        /// May be null for screens that do not use tool assembly.
        /// </summary>
        public ToolTraitRegistry ToolTraitRegistry
        {
            get { return _toolTraitRegistry; }
        }

        public ScreenContext(
            Inventory playerInventory,
            ItemRegistry itemRegistry,
            ItemSpriteAtlas itemSpriteAtlas,
            PanelSettings panelSettings,
            CraftingEngine craftingEngine,
            ToolTraitRegistry toolTraitRegistry)
        {
            _playerInventory = playerInventory;
            _itemRegistry = itemRegistry;
            _itemSpriteAtlas = itemSpriteAtlas;
            _panelSettings = panelSettings;
            _craftingEngine = craftingEngine;
            _toolTraitRegistry = toolTraitRegistry;
        }
    }
}