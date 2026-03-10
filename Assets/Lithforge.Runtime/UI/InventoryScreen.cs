using Lithforge.Core.Data;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Full inventory screen opened with E key.
    /// Shows 36 inventory slots, a 2x2 crafting grid, and a craft output slot.
    /// Supports click-to-swap between slots.
    /// Uses UI Toolkit with code-driven VisualElement construction.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const int _slotSize = 60;
        private const int _slotMargin = 3;
        private const int _borderWidth = 2;
        private const int _slotsPerRow = 9;

        private UIDocument _document;
        private Inventory _inventory;
        private ItemRegistry _itemRegistry;
        private CraftingEngine _craftingEngine;

        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement[] _invSlotElements;
        private Label[] _invNameLabels;
        private Label[] _invCountLabels;

        private VisualElement[,] _craftSlotElements;
        private Label[,] _craftNameLabels;
        private Label[,] _craftCountLabels;

        private VisualElement _outputSlotElement;
        private Label _outputNameLabel;
        private Label _outputCountLabel;

        private CraftingGrid _craftingGrid;

        // Held item (cursor item for click-to-swap)
        private ItemStack _heldItem;
        private Label _heldLabel;
        private VisualElement _heldElement;

        private bool _isOpen;

        private static readonly Color _slotBackground = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        private static readonly Color _slotBorder = new Color(0.45f, 0.45f, 0.45f, 0.9f);
        private static readonly Color _slotHover = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        private static readonly Color _panelBackground = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        private static readonly Color _sectionLabel = new Color(0.7f, 0.7f, 0.7f, 1f);

        public bool IsOpen
        {
            get { return _isOpen; }
        }

        /// <summary>
        /// Shows or hides the entire inventory system by toggling the root document visibility.
        /// When hidden, the E key toggle in Update() still runs but has no visible effect.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Initialize(
            Inventory inventory,
            ItemRegistry itemRegistry,
            CraftingEngine craftingEngine,
            PanelSettings panelSettings)
        {
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _craftingEngine = craftingEngine;
            _craftingGrid = new CraftingGrid(2, 2);

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 200;

            _root = _document.rootVisualElement;

            BuildInventoryUI(_root);
            _panel.style.display = DisplayStyle.None;
            _isOpen = false;
        }

        private void BuildInventoryUI(VisualElement root)
        {
            // Full-screen overlay
            _panel = new VisualElement();
            _panel.name = "inventory-panel";
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0;
            _panel.style.top = 0;
            _panel.style.right = 0;
            _panel.style.bottom = 0;
            _panel.style.alignItems = Align.Center;
            _panel.style.justifyContent = Justify.Center;
            _panel.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            root.Add(_panel);

            // Main container
            VisualElement container = new VisualElement();
            container.name = "inventory-container";
            container.style.backgroundColor = _panelBackground;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            _panel.Add(container);

            // Title
            Label title = new Label("Inventory");
            title.style.fontSize = 20;
            title.style.color = Color.white;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 8;
            container.Add(title);

            // Crafting section
            VisualElement craftSection = new VisualElement();
            craftSection.style.flexDirection = FlexDirection.Row;
            craftSection.style.alignItems = Align.Center;
            craftSection.style.justifyContent = Justify.Center;
            craftSection.style.marginBottom = 12;
            container.Add(craftSection);

            // 2x2 crafting grid
            VisualElement craftGrid = new VisualElement();
            _craftSlotElements = new VisualElement[2, 2];
            _craftNameLabels = new Label[2, 2];
            _craftCountLabels = new Label[2, 2];

            for (int y = 0; y < 2; y++)
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                for (int x = 0; x < 2; x++)
                {
                    int cx = x;
                    int cy = y;
                    VisualElement slot = CreateSlot();
                    Label nameLabel = CreateNameLabel();
                    Label countLabel = CreateCountLabel();
                    slot.Add(nameLabel);
                    slot.Add(countLabel);
                    slot.RegisterCallback<ClickEvent>(evt => OnCraftSlotClick(cx, cy));
                    row.Add(slot);

                    _craftSlotElements[x, y] = slot;
                    _craftNameLabels[x, y] = nameLabel;
                    _craftCountLabels[x, y] = countLabel;
                }

                craftGrid.Add(row);
            }

            craftSection.Add(craftGrid);

            // Arrow
            Label arrow = new Label("  =>  ");
            arrow.style.fontSize = 24;
            arrow.style.color = Color.white;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            craftSection.Add(arrow);

            // Output slot
            _outputSlotElement = CreateSlot();
            _outputNameLabel = CreateNameLabel();
            _outputCountLabel = CreateCountLabel();
            _outputSlotElement.Add(_outputNameLabel);
            _outputSlotElement.Add(_outputCountLabel);
            _outputSlotElement.RegisterCallback<ClickEvent>(evt => OnOutputSlotClick());
            craftSection.Add(_outputSlotElement);

            // Separator
            VisualElement sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            sep.style.marginTop = 4;
            sep.style.marginBottom = 8;
            container.Add(sep);

            // Main inventory (27 slots in 3 rows of 9)
            _invSlotElements = new VisualElement[Inventory.SlotCount];
            _invNameLabels = new Label[Inventory.SlotCount];
            _invCountLabels = new Label[Inventory.SlotCount];

            Label mainLabel = new Label("Main");
            mainLabel.style.fontSize = 14;
            mainLabel.style.color = _sectionLabel;
            mainLabel.style.marginBottom = 4;
            container.Add(mainLabel);

            for (int row = 0; row < 3; row++)
            {
                VisualElement rowElement = new VisualElement();
                rowElement.style.flexDirection = FlexDirection.Row;

                for (int col = 0; col < _slotsPerRow; col++)
                {
                    int slotIndex = 9 + row * _slotsPerRow + col;
                    int idx = slotIndex;
                    VisualElement slot = CreateSlot();
                    Label nameLabel = CreateNameLabel();
                    Label countLabel = CreateCountLabel();
                    slot.Add(nameLabel);
                    slot.Add(countLabel);
                    slot.RegisterCallback<ClickEvent>(evt => OnInventorySlotClick(idx));
                    rowElement.Add(slot);

                    _invSlotElements[slotIndex] = slot;
                    _invNameLabels[slotIndex] = nameLabel;
                    _invCountLabels[slotIndex] = countLabel;
                }

                container.Add(rowElement);
            }

            // Separator before hotbar
            VisualElement sep2 = new VisualElement();
            sep2.style.height = 1;
            sep2.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            sep2.style.marginTop = 8;
            sep2.style.marginBottom = 4;
            container.Add(sep2);

            Label hotbarLabel = new Label("Hotbar");
            hotbarLabel.style.fontSize = 14;
            hotbarLabel.style.color = _sectionLabel;
            hotbarLabel.style.marginBottom = 4;
            container.Add(hotbarLabel);

            // Hotbar row (slots 0-8)
            VisualElement hotbarRow = new VisualElement();
            hotbarRow.style.flexDirection = FlexDirection.Row;

            for (int col = 0; col < _slotsPerRow; col++)
            {
                int slotIndex = col;
                int idx = slotIndex;
                VisualElement slot = CreateSlot();
                Label nameLabel = CreateNameLabel();
                Label countLabel = CreateCountLabel();
                slot.Add(nameLabel);
                slot.Add(countLabel);
                slot.RegisterCallback<ClickEvent>(evt => OnInventorySlotClick(idx));
                hotbarRow.Add(slot);

                _invSlotElements[slotIndex] = slot;
                _invNameLabels[slotIndex] = nameLabel;
                _invCountLabels[slotIndex] = countLabel;
            }

            container.Add(hotbarRow);

            // Held item display (follows cursor concept — shown as text at bottom)
            _heldElement = new VisualElement();
            _heldElement.name = "held-item";
            _heldElement.style.position = Position.Absolute;
            _heldElement.style.bottom = 4;
            _heldElement.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            _heldElement.style.marginLeft = -100;
            _heldElement.style.width = 200;
            _heldElement.style.alignItems = Align.Center;
            _heldElement.style.display = DisplayStyle.None;

            _heldLabel = new Label("");
            _heldLabel.style.fontSize = 14;
            _heldLabel.style.color = new Color(1f, 1f, 0.5f, 1f);
            _heldLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _heldElement.Add(_heldLabel);
            _panel.Add(_heldElement);
        }

        private VisualElement CreateSlot()
        {
            VisualElement slot = new VisualElement();
            slot.style.width = _slotSize;
            slot.style.height = _slotSize;
            slot.style.marginLeft = _slotMargin;
            slot.style.marginRight = _slotMargin;
            slot.style.marginTop = _slotMargin;
            slot.style.marginBottom = _slotMargin;
            slot.style.backgroundColor = _slotBackground;
            slot.style.borderTopWidth = _borderWidth;
            slot.style.borderBottomWidth = _borderWidth;
            slot.style.borderLeftWidth = _borderWidth;
            slot.style.borderRightWidth = _borderWidth;
            slot.style.borderTopColor = _slotBorder;
            slot.style.borderBottomColor = _slotBorder;
            slot.style.borderLeftColor = _slotBorder;
            slot.style.borderRightColor = _slotBorder;
            slot.style.justifyContent = Justify.Center;
            slot.style.alignItems = Align.Center;

            slot.RegisterCallback<MouseEnterEvent>(evt =>
            {
                slot.style.backgroundColor = _slotHover;
            });
            slot.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                slot.style.backgroundColor = _slotBackground;
            });

            return slot;
        }

        private Label CreateNameLabel()
        {
            Label label = new Label("");
            label.style.fontSize = 12;
            label.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.maxWidth = _slotSize - 4;
            label.pickingMode = PickingMode.Ignore;

            return label;
        }

        private Label CreateCountLabel()
        {
            Label label = new Label("");
            label.style.position = Position.Absolute;
            label.style.bottom = 2;
            label.style.right = 4;
            label.style.fontSize = 13;
            label.style.color = Color.white;
            label.style.unityTextAlign = TextAnchor.LowerRight;
            label.pickingMode = PickingMode.Ignore;

            return label;
        }

        private void Update()
        {
            if (_inventory == null)
            {
                return;
            }

            // Toggle with E key
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }

            if (!_isOpen)
            {
                return;
            }

            // Update inventory slot displays
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                if (_invSlotElements[i] == null)
                {
                    continue;
                }

                ItemStack stack = _inventory.GetSlot(i);
                UpdateSlotDisplay(_invNameLabels[i], _invCountLabels[i], stack);
            }

            // Update crafting grid displays
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ResourceId craftItem = _craftingGrid.GetSlot(x, y);

                    if (craftItem.Namespace != null)
                    {
                        string displayName = ItemDisplayFormatter.FormatItemName(craftItem.Name);
                        _craftNameLabels[x, y].text = displayName;
                        _craftCountLabels[x, y].text = "1";
                    }
                    else
                    {
                        _craftNameLabels[x, y].text = "";
                        _craftCountLabels[x, y].text = "";
                    }
                }
            }

            // Update craft output
            RecipeEntry match = _craftingEngine.FindMatch(_craftingGrid);

            if (match != null)
            {
                _outputNameLabel.text = ItemDisplayFormatter.FormatItemName(match.ResultItem.Name);
                _outputCountLabel.text = match.ResultCount > 1 ? match.ResultCount.ToString() : "";
            }
            else
            {
                _outputNameLabel.text = "";
                _outputCountLabel.text = "";
            }

            // Update held item display
            if (_heldItem.IsEmpty)
            {
                _heldElement.style.display = DisplayStyle.None;
            }
            else
            {
                _heldElement.style.display = DisplayStyle.Flex;
                _heldLabel.text = $"Holding: {_heldItem.ItemId.Name} x{_heldItem.Count}";
            }
        }

        private void OnInventorySlotClick(int slotIndex)
        {
            if (!_isOpen)
            {
                return;
            }

            ItemStack slotItem = _inventory.GetSlot(slotIndex);

            if (_heldItem.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            // Swap held with slot
            _inventory.SetSlot(slotIndex, _heldItem);
            _heldItem = slotItem;
        }

        private void OnCraftSlotClick(int x, int y)
        {
            if (!_isOpen)
            {
                return;
            }

            ResourceId currentCraft = _craftingGrid.GetSlot(x, y);

            if (_heldItem.IsEmpty && currentCraft.Namespace == null)
            {
                return;
            }

            if (!_heldItem.IsEmpty)
            {
                // Place held item into craft grid, return current craft item to held
                if (currentCraft.Namespace != null)
                {
                    // Swap: return craft item to held
                    ResourceId oldCraft = currentCraft;
                    _craftingGrid.SetSlot(x, y, _heldItem.ItemId);

                    // Remove one from held
                    ItemStack updated = _heldItem;
                    updated.Count -= 1;
                    _heldItem = updated.IsEmpty ? ItemStack.Empty : updated;

                    // Give back old craft item
                    if (_heldItem.IsEmpty)
                    {
                        _heldItem = new ItemStack(oldCraft, 1);
                    }
                    else
                    {
                        // Try to add to inventory, pick up as held if full
                        ItemEntry def = _itemRegistry.Get(oldCraft);
                        int maxStack = def != null ? def.MaxStackSize : 64;
                        int leftOver = _inventory.AddItem(oldCraft, 1, maxStack);

                        if (leftOver > 0)
                        {
                            // Inventory full — revert the swap
                            _craftingGrid.SetSlot(x, y, oldCraft);
                            ItemStack reverted = _heldItem;
                            reverted.Count += 1;
                            _heldItem = reverted;

                            return;
                        }
                    }
                }
                else
                {
                    // Place one item from held
                    _craftingGrid.SetSlot(x, y, _heldItem.ItemId);
                    ItemStack updated = _heldItem;
                    updated.Count -= 1;
                    _heldItem = updated.IsEmpty ? ItemStack.Empty : updated;
                }
            }
            else
            {
                // Pick up craft grid item
                _heldItem = new ItemStack(currentCraft, 1);
                _craftingGrid.SetSlot(x, y, default);
            }
        }

        private void OnOutputSlotClick()
        {
            if (!_isOpen)
            {
                return;
            }

            RecipeEntry match = _craftingEngine.FindMatch(_craftingGrid);

            if (match == null)
            {
                return;
            }

            // Add result to held or inventory
            ItemEntry resultDef = _itemRegistry.Get(match.ResultItem);
            int durability = (resultDef != null && resultDef.Durability > 0)
                ? resultDef.Durability
                : -1;

            if (_heldItem.IsEmpty)
            {
                _heldItem = new ItemStack(match.ResultItem, match.ResultCount, durability);
            }
            else if (_heldItem.ItemId == match.ResultItem)
            {
                ItemEntry def = _itemRegistry.Get(match.ResultItem);
                int maxStack = def != null ? def.MaxStackSize : 64;

                if (_heldItem.Count + match.ResultCount <= maxStack)
                {
                    ItemStack updated = _heldItem;
                    updated.Count += match.ResultCount;
                    _heldItem = updated;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            // Consume one item from each craft grid slot
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ResourceId gridItem = _craftingGrid.GetSlot(x, y);

                    if (gridItem.Namespace != null)
                    {
                        _craftingGrid.SetSlot(x, y, default);
                    }
                }
            }
        }

        public void Open()
        {
            _isOpen = true;
            _panel.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            _isOpen = false;
            _panel.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Return held item to inventory (only clear if it fit)
            if (!_heldItem.IsEmpty)
            {
                ItemEntry def = _itemRegistry.Get(_heldItem.ItemId);
                int maxStack = def != null ? def.MaxStackSize : 64;
                int leftOver = _inventory.AddItem(_heldItem.ItemId, _heldItem.Count, maxStack);

                if (leftOver == 0)
                {
                    _heldItem = ItemStack.Empty;
                }
                else
                {
                    ItemStack partial = _heldItem;
                    partial.Count = leftOver;
                    _heldItem = partial;
                }
            }

            // Return crafting grid items to inventory (only clear if they fit)
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ResourceId gridItem = _craftingGrid.GetSlot(x, y);

                    if (gridItem.Namespace != null)
                    {
                        ItemEntry def = _itemRegistry.Get(gridItem);
                        int maxStack = def != null ? def.MaxStackSize : 64;
                        int leftOver = _inventory.AddItem(gridItem, 1, maxStack);

                        if (leftOver == 0)
                        {
                            _craftingGrid.SetSlot(x, y, default);
                        }
                    }
                }
            }
        }

        private void UpdateSlotDisplay(Label nameLabel, Label countLabel, ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                nameLabel.text = "";
                countLabel.text = "";
            }
            else
            {
                nameLabel.text = ItemDisplayFormatter.FormatItemName(stack.ItemId.Name);
                countLabel.text = stack.Count > 1 ? stack.Count.ToString() : "";
            }
        }

    }
}
