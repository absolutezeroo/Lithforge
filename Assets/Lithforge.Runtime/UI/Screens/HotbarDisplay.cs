using System;

using Lithforge.Item;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Runtime.UI.Widgets;
using Lithforge.Voxel.Item;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Read-only hotbar display at the bottom center of the screen.
    ///     Shows 9 slots with item icons, selected slot highlight, and item name banner.
    /// </summary>
    public sealed class HotbarDisplay : MonoBehaviour, IScreen
    {
        /// <summary>UI Toolkit document hosting the hotbar panel.</summary>
        private UIDocument _document;

        /// <summary>Reference to the player inventory for reading hotbar slot contents.</summary>
        private Inventory _inventory;

        /// <summary>Item registry for looking up item definitions during display refresh.</summary>
        private ItemRegistry _itemRegistry;

        /// <summary>Last selected hotbar slot index, used to detect selection changes.</summary>
        private int _lastSelectedSlot = -1;

        /// <summary>Animated banner that shows the selected item name above the hotbar.</summary>
        private ItemNameBanner _nameBanner;

        /// <summary>Array of slot widgets representing the 9 hotbar positions.</summary>
        private SlotWidget[] _slotWidgets;

        /// <summary>Sprite atlas for resolving item icons in slot widgets.</summary>
        private ItemSpriteAtlas _spriteAtlas;

        /// <summary>Tool part texture database for resolving modular tool part sprites.</summary>
        private ToolPartTextureDatabase _toolPartTexDb;

        /// <summary>Updates slot selection highlight, fades the name banner, and refreshes item icons each frame.</summary>
        private void Update()
        {
            if (_inventory == null)
            {
                return;
            }

            int selectedSlot = _inventory.SelectedSlot;

            // Update selection highlight
            if (selectedSlot != _lastSelectedSlot)
            {
                for (int i = 0; i < Inventory.HotbarSize; i++)
                {
                    _slotWidgets[i].SetSelected(i == selectedSlot);
                }

                ItemStack selectedStack = _inventory.GetSlot(selectedSlot);

                if (!selectedStack.IsEmpty)
                {
                    _nameBanner.ShowName(selectedStack.ItemId.Name);
                }
                else
                {
                    _nameBanner.ShowName(null);
                }

                _lastSelectedSlot = selectedSlot;
            }

            // Fade banner
            _nameBanner.Tick(Time.deltaTime);

            // Update item display
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                ItemStack stack = _inventory.GetSlot(i);
                _slotWidgets[i].Refresh(stack, _spriteAtlas, _itemRegistry, _toolPartTexDb);
            }
        }

        /// <summary>Returns the screen name identifier for the hotbar.</summary>
        public string ScreenName { get { return ScreenNames.Hotbar; } }

        /// <summary>Returns false because the hotbar does not consume input.</summary>
        public bool IsInputOpaque { get { return false; } }

        /// <summary>Returns false because the hotbar does not require a visible cursor.</summary>
        public bool RequiresCursor { get { return false; } }

        /// <summary>Shows the hotbar when the screen is pushed onto the navigation stack.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            SetVisible(true);
        }

        /// <summary>Hides the hotbar and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            SetVisible(false);
            onComplete();
        }

        /// <summary>Returns false because the hotbar does not handle the Escape key.</summary>
        public bool HandleEscape()
        {
            return false;
        }

        /// <summary>Initializes the hotbar with inventory, registry references, and builds the slot widget UI.</summary>
        public void Initialize(
            Inventory inventory,
            PanelSettings panelSettings,
            ItemRegistry itemRegistry,
            ItemSpriteAtlas spriteAtlas,
            ToolPartTextureDatabase toolPartTexDb = null)
        {
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _spriteAtlas = spriteAtlas;
            _toolPartTexDb = toolPartTexDb;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 50;

            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Load USS themes
            StyleSheet variables = Resources.Load<StyleSheet>("UI/Themes/LithforgeVariables");
            StyleSheet theme = Resources.Load<StyleSheet>("UI/Themes/LithforgeDefault");

            if (variables != null)
            {
                root.styleSheets.Add(variables);
            }

            if (theme != null)
            {
                root.styleSheets.Add(theme);
            }

            BuildHotbar(root);
        }

        /// <summary>Creates the hotbar container with 9 slot widgets and an item name banner.</summary>
        private void BuildHotbar(VisualElement root)
        {
            _slotWidgets = new SlotWidget[Inventory.HotbarSize];

            // Hotbar container at bottom center
            VisualElement hotbar = new();
            hotbar.AddToClassList("lf-hotbar");
            hotbar.pickingMode = PickingMode.Ignore;
            int totalWidth = Inventory.HotbarSize * (60 + 6); // slot size + margins
            hotbar.style.marginLeft = -(totalWidth / 2);
            root.Add(hotbar);

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                SlotWidget widget = new()
                {
                    pickingMode = PickingMode.Ignore,
                };

                // Make all children ignore picks too
                widget.Query().ForEach(child =>
                {
                    child.pickingMode = PickingMode.Ignore;
                });

                hotbar.Add(widget);
                _slotWidgets[i] = widget;
            }

            // Item name banner above hotbar
            _nameBanner = new ItemNameBanner();
            root.Add(_nameBanner);
        }

        /// <summary>
        ///     Shows or hides the hotbar by toggling the root document visibility.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
