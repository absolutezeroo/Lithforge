using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Runtime.UI.Widgets;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Read-only hotbar display at the bottom center of the screen.
    /// Shows 9 slots with item icons, selected slot highlight, and item name banner.
    /// </summary>
    public sealed class HotbarDisplay : MonoBehaviour
    {
        private UIDocument _document;
        private Inventory _inventory;
        private ItemRegistry _itemRegistry;
        private ItemSpriteAtlas _spriteAtlas;
        private ToolPartTextureDatabase _toolPartTexDb;

        private SlotWidget[] _slotWidgets;
        private ItemNameBanner _nameBanner;

        private int _lastSelectedSlot = -1;

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

        private void BuildHotbar(VisualElement root)
        {
            _slotWidgets = new SlotWidget[Inventory.HotbarSize];

            // Hotbar container at bottom center
            VisualElement hotbar = new VisualElement();
            hotbar.AddToClassList("lf-hotbar");
            hotbar.pickingMode = PickingMode.Ignore;
            int totalWidth = Inventory.HotbarSize * (60 + 6); // slot size + margins
            hotbar.style.marginLeft = -(totalWidth / 2);
            root.Add(hotbar);

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                SlotWidget widget = new SlotWidget();
                widget.pickingMode = PickingMode.Ignore;

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

        /// <summary>
        /// Shows or hides the hotbar by toggling the root document visibility.
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
