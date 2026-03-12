using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Full inventory screen opened with E key.
    /// Shows 36 inventory slots, a 2x2 crafting grid, and a craft output slot.
    /// Supports left-click (pick/place/merge/swap), right-click (pick half/place 1),
    /// right-click drag (paint mode), Shift+click (quick transfer), and number keys 1-9
    /// (hotbar swap with hovered slot).
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

        // Cached slot state for dirty-checking (avoids updating unchanged slots)
        private ItemStack[] _lastSlotState;

        private bool _isOpen;

        // Hover tracking — only inventory slots (0-35), -1 = none
        private int _hoveredSlotIndex = -1;

        // Right-click drag paint mode
        private bool _isPainting;
        private bool _paintPending;
        private int _paintOriginSlot;
        private ResourceId _paintItemId;
        private int _paintDurability;
        private HashSet<int> _paintedSlots = new HashSet<int>();

        // Number key mappings for hotbar swap
        private static readonly Key[] _numberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

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
            _lastSlotState = new ItemStack[Inventory.SlotCount];

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

            // Stop right-click paint mode on pointer up
            _panel.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    _isPainting = false;
                    _paintPending = false;
                    _paintedSlots.Clear();
                }
            });

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
                    slot.RegisterCallback<PointerDownEvent>(evt => OnCraftSlotPointerDown(cx, cy, evt));
                    RegisterBasicHover(slot);
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
            _outputSlotElement.RegisterCallback<PointerDownEvent>(evt => OnOutputSlotPointerDown(evt));
            RegisterBasicHover(_outputSlotElement);
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
                    slot.userData = idx;
                    Label nameLabel = CreateNameLabel();
                    Label countLabel = CreateCountLabel();
                    slot.Add(nameLabel);
                    slot.Add(countLabel);
                    slot.RegisterCallback<PointerDownEvent>(evt => OnInventorySlotPointerDown(idx, evt));
                    RegisterInventorySlotHover(slot, idx);
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
                slot.userData = idx;
                Label nameLabel = CreateNameLabel();
                Label countLabel = CreateCountLabel();
                slot.Add(nameLabel);
                slot.Add(countLabel);
                slot.RegisterCallback<PointerDownEvent>(evt => OnInventorySlotPointerDown(idx, evt));
                RegisterInventorySlotHover(slot, idx);
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

        /// <summary>
        /// Registers PointerEnterEvent/PointerLeaveEvent on an inventory slot for hover tracking
        /// and paint mode. Only inventory slots (index >= 0) update _hoveredSlotIndex.
        /// </summary>
        private void RegisterInventorySlotHover(VisualElement slot, int slotIndex)
        {
            slot.RegisterCallback<PointerEnterEvent>(evt =>
            {
                slot.style.backgroundColor = _slotHover;
                _hoveredSlotIndex = slotIndex;
                OnInventorySlotHovered(slotIndex);
            });
            slot.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                slot.style.backgroundColor = _slotBackground;
                if (_hoveredSlotIndex == slotIndex)
                {
                    _hoveredSlotIndex = -1;
                }
            });
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

            return slot;
        }

        /// <summary>
        /// Registers basic hover highlight on a slot that does not need inventory tracking
        /// (craft slots, output slot).
        /// </summary>
        private void RegisterBasicHover(VisualElement slot)
        {
            slot.RegisterCallback<PointerEnterEvent>(evt =>
            {
                slot.style.backgroundColor = _slotHover;
            });
            slot.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                slot.style.backgroundColor = _slotBackground;
            });
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
            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
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

            // Handle number keys 1-9 for hotbar swap
            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            // Update only changed inventory slot displays
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                RefreshSlot(i);
            }

            // Update crafting grid displays
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ItemStack craftStack = _craftingGrid.GetSlotStack(x, y);

                    if (!craftStack.IsEmpty)
                    {
                        _craftNameLabels[x, y].text = ItemDisplayFormatter.FormatItemName(craftStack.ItemId.Name);
                        _craftCountLabels[x, y].text = craftStack.Count > 1 ? craftStack.Count.ToString() : "";
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

        // ────────────────────────────────────────────────────────────────────
        // Inventory slot handlers
        // ────────────────────────────────────────────────────────────────────

        private void OnInventorySlotPointerDown(int slotIndex, PointerDownEvent evt)
        {
            if (!_isOpen)
            {
                return;
            }

            // Shift+left-click: quick transfer
            if (evt.button == 0 && Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
            {
                ShiftClickInventorySlot(slotIndex);
                evt.StopPropagation();
                return;
            }

            ItemStack slotItem = _inventory.GetSlot(slotIndex);
            ItemEntry slotDef = !slotItem.IsEmpty ? _itemRegistry.Get(slotItem.ItemId) : null;
            int slotMaxStack = slotDef != null ? slotDef.MaxStackSize : 64;

            if (evt.button == 0)
            {
                LeftClickInventorySlot(slotIndex, slotItem, slotMaxStack);
            }
            else if (evt.button == 1)
            {
                RightClickInventorySlot(slotIndex, slotItem, slotMaxStack);
            }

            evt.StopPropagation();
        }

        private void LeftClickInventorySlot(int slotIndex, ItemStack slotItem, int slotMaxStack)
        {
            if (_heldItem.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (_heldItem.IsEmpty)
            {
                // Pick up entire stack
                _heldItem = slotItem;
                _inventory.SetSlot(slotIndex, ItemStack.Empty);
            }
            else if (slotItem.IsEmpty)
            {
                // Place entire held stack
                _inventory.SetSlot(slotIndex, _heldItem);
                _heldItem = ItemStack.Empty;
            }
            else if (_heldItem.ItemId == slotItem.ItemId)
            {
                // Merge held into slot
                int space = slotMaxStack - slotItem.Count;

                if (space <= 0)
                {
                    // Slot full, swap
                    _inventory.SetSlot(slotIndex, _heldItem);
                    _heldItem = slotItem;
                }
                else
                {
                    int toMove = Mathf.Min(_heldItem.Count, space);
                    ItemStack newSlot = slotItem;
                    newSlot.Count += toMove;
                    _inventory.SetSlot(slotIndex, newSlot);

                    ItemStack newHeld = _heldItem;
                    newHeld.Count -= toMove;
                    _heldItem = newHeld.IsEmpty ? ItemStack.Empty : newHeld;
                }
            }
            else
            {
                // Swap different items
                _inventory.SetSlot(slotIndex, _heldItem);
                _heldItem = slotItem;
            }
        }

        private void RightClickInventorySlot(int slotIndex, ItemStack slotItem, int slotMaxStack)
        {
            if (_heldItem.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (_heldItem.IsEmpty)
            {
                // Pick up half (rounded up)
                int half = (slotItem.Count + 1) / 2;
                _heldItem = new ItemStack(slotItem.ItemId, half, slotItem.Durability);

                ItemStack remaining = slotItem;
                remaining.Count -= half;
                _inventory.SetSlot(slotIndex, remaining.IsEmpty ? ItemStack.Empty : remaining);
            }
            else if (slotItem.IsEmpty)
            {
                // Place 1
                _inventory.SetSlot(slotIndex, new ItemStack(_heldItem.ItemId, 1, _heldItem.Durability));

                ItemStack newHeld = _heldItem;
                newHeld.Count -= 1;
                _heldItem = newHeld.IsEmpty ? ItemStack.Empty : newHeld;

                if (!_heldItem.IsEmpty)
                {
                    StartPaint(slotIndex);
                }
            }
            else if (_heldItem.ItemId == slotItem.ItemId && slotItem.Count < slotMaxStack)
            {
                // Place 1 on same item
                ItemStack newSlot = slotItem;
                newSlot.Count += 1;
                _inventory.SetSlot(slotIndex, newSlot);

                ItemStack newHeld = _heldItem;
                newHeld.Count -= 1;
                _heldItem = newHeld.IsEmpty ? ItemStack.Empty : newHeld;

                if (!_heldItem.IsEmpty)
                {
                    StartPaint(slotIndex);
                }
            }
            // Right click on different item = nothing (Minecraft behavior)
        }

        // ────────────────────────────────────────────────────────────────────
        // Craft slot handlers
        // ────────────────────────────────────────────────────────────────────

        private void OnCraftSlotPointerDown(int x, int y, PointerDownEvent evt)
        {
            if (!_isOpen)
            {
                return;
            }

            ItemStack craftStack = _craftingGrid.GetSlotStack(x, y);

            if (evt.button == 0)
            {
                LeftClickCraftSlot(x, y, craftStack);
            }
            else if (evt.button == 1)
            {
                RightClickCraftSlot(x, y, craftStack);
            }

            evt.StopPropagation();
        }

        private void LeftClickCraftSlot(int x, int y, ItemStack craftStack)
        {
            if (_heldItem.IsEmpty && craftStack.IsEmpty)
            {
                return;
            }

            if (_heldItem.IsEmpty)
            {
                // Pick up entire craft stack
                _heldItem = craftStack;
                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
            }
            else if (craftStack.IsEmpty)
            {
                // Place entire held stack
                _craftingGrid.SetSlotStack(x, y, _heldItem);
                _heldItem = ItemStack.Empty;
            }
            else if (_heldItem.ItemId == craftStack.ItemId)
            {
                // Merge
                ItemEntry def = _itemRegistry.Get(_heldItem.ItemId);
                int max = def != null ? def.MaxStackSize : 64;
                int space = max - craftStack.Count;

                if (space <= 0)
                {
                    // Swap
                    ItemStack tmp = craftStack;
                    _craftingGrid.SetSlotStack(x, y, _heldItem);
                    _heldItem = tmp;
                }
                else
                {
                    int toMove = Mathf.Min(_heldItem.Count, space);
                    ItemStack newCraft = craftStack;
                    newCraft.Count += toMove;
                    _craftingGrid.SetSlotStack(x, y, newCraft);

                    ItemStack newHeld = _heldItem;
                    newHeld.Count -= toMove;
                    _heldItem = newHeld.IsEmpty ? ItemStack.Empty : newHeld;
                }
            }
            else
            {
                // Swap different items
                _craftingGrid.SetSlotStack(x, y, _heldItem);
                _heldItem = craftStack;
            }
        }

        private void RightClickCraftSlot(int x, int y, ItemStack craftStack)
        {
            if (_heldItem.IsEmpty && craftStack.IsEmpty)
            {
                return;
            }

            if (_heldItem.IsEmpty)
            {
                // Pick up half
                int half = (craftStack.Count + 1) / 2;
                _heldItem = new ItemStack(craftStack.ItemId, half, craftStack.Durability);
                ItemStack remaining = craftStack;
                remaining.Count -= half;
                _craftingGrid.SetSlotStack(x, y, remaining.IsEmpty ? ItemStack.Empty : remaining);
            }
            else if (craftStack.IsEmpty)
            {
                // Place 1
                _craftingGrid.SetSlotStack(x, y, new ItemStack(_heldItem.ItemId, 1, _heldItem.Durability));
                ItemStack h = _heldItem;
                h.Count -= 1;
                _heldItem = h.IsEmpty ? ItemStack.Empty : h;
            }
            else if (_heldItem.ItemId == craftStack.ItemId)
            {
                ItemEntry def = _itemRegistry.Get(_heldItem.ItemId);
                int max = def != null ? def.MaxStackSize : 64;

                if (craftStack.Count < max)
                {
                    ItemStack newCraft = craftStack;
                    newCraft.Count += 1;
                    _craftingGrid.SetSlotStack(x, y, newCraft);
                    ItemStack h = _heldItem;
                    h.Count -= 1;
                    _heldItem = h.IsEmpty ? ItemStack.Empty : h;
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Output slot handlers
        // ────────────────────────────────────────────────────────────────────

        private void OnOutputSlotPointerDown(PointerDownEvent evt)
        {
            if (!_isOpen)
            {
                return;
            }

            if (evt.button == 0 && Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
            {
                ShiftClickCraftOutput();
            }
            else if (evt.button == 0)
            {
                OnOutputSlotClick();
            }

            evt.StopPropagation();
        }

        private void OnOutputSlotClick()
        {
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

            // Consume one item from each non-empty craft grid slot
            ConsumeCraftGridIngredients();
        }

        private void ShiftClickCraftOutput()
        {
            // Craft max iterations (capped at 64 to prevent infinite loop)
            for (int i = 0; i < 64; i++)
            {
                RecipeEntry match = _craftingEngine.FindMatch(_craftingGrid);

                if (match == null)
                {
                    break;
                }

                ItemEntry resultDef = _itemRegistry.Get(match.ResultItem);
                int maxStack = resultDef != null ? resultDef.MaxStackSize : 64;
                int durability = (resultDef != null && resultDef.Durability > 0)
                    ? resultDef.Durability
                    : -1;

                // Tools (durability > 0) need special handling to preserve durability
                if (durability > 0)
                {
                    int leftOver = _inventory.AddItemWithDurability(match.ResultItem, durability);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }
                else
                {
                    int leftOver = _inventory.AddItem(match.ResultItem, match.ResultCount, maxStack);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }

                // Consume ingredients
                ConsumeCraftGridIngredients();
            }
        }

        /// <summary>
        /// Consumes one item from each non-empty slot in the crafting grid.
        /// </summary>
        private void ConsumeCraftGridIngredients()
        {
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ItemStack gridStack = _craftingGrid.GetSlotStack(x, y);

                    if (!gridStack.IsEmpty)
                    {
                        ItemStack consumed = gridStack;
                        consumed.Count -= 1;
                        _craftingGrid.SetSlotStack(x, y, consumed.IsEmpty ? ItemStack.Empty : consumed);
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Paint mode (right-click drag)
        // ────────────────────────────────────────────────────────────────────

        private void StartPaint(int slotIndex)
        {
            _isPainting = false;
            _paintPending = true;
            _paintOriginSlot = slotIndex;
            _paintItemId = _heldItem.IsEmpty ? default : _heldItem.ItemId;
            _paintDurability = _heldItem.Durability;
            _paintedSlots.Clear();
            _paintedSlots.Add(slotIndex);
        }

        private void OnInventorySlotHovered(int slotIndex)
        {
            // Activate pending paint when pointer enters a different slot than origin
            if (_paintPending && slotIndex != _paintOriginSlot)
            {
                _isPainting = true;
                _paintPending = false;
            }

            if (!_isPainting || slotIndex < 0)
            {
                return;
            }

            if (_paintedSlots.Contains(slotIndex))
            {
                return;
            }

            if (_heldItem.IsEmpty)
            {
                _isPainting = false;
                _paintPending = false;
                _paintedSlots.Clear();
                return;
            }

            ItemStack slotItem = _inventory.GetSlot(slotIndex);

            // Can place 1 if: slot empty OR slot has same item and isn't full
            bool canPlace = false;

            if (slotItem.IsEmpty)
            {
                canPlace = true;
            }
            else if (slotItem.ItemId == _paintItemId)
            {
                ItemEntry def = _itemRegistry.Get(_paintItemId);
                int max = def != null ? def.MaxStackSize : 64;
                canPlace = slotItem.Count < max;
            }

            if (!canPlace)
            {
                return;
            }

            // Place 1
            if (slotItem.IsEmpty)
            {
                _inventory.SetSlot(slotIndex, new ItemStack(_paintItemId, 1, _paintDurability));
            }
            else
            {
                ItemStack updated = slotItem;
                updated.Count += 1;
                _inventory.SetSlot(slotIndex, updated);
            }

            ItemStack h = _heldItem;
            h.Count -= 1;
            _heldItem = h.IsEmpty ? ItemStack.Empty : h;
            _paintedSlots.Add(slotIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // Number keys (hotbar swap)
        // ────────────────────────────────────────────────────────────────────

        private void HandleNumberKeys(Keyboard keyboard)
        {
            if (_hoveredSlotIndex < 0)
            {
                return;
            }

            for (int i = 0; i < _numberKeys.Length; i++)
            {
                if (!keyboard[_numberKeys[i]].wasPressedThisFrame)
                {
                    continue;
                }

                int hotbarSlot = i; // 0-8

                if (_hoveredSlotIndex == hotbarSlot)
                {
                    return;
                }

                ItemStack hotbarItem = _inventory.GetSlot(hotbarSlot);
                ItemStack hoveredItem = _inventory.GetSlot(_hoveredSlotIndex);

                _inventory.SetSlot(hotbarSlot, hoveredItem);
                _inventory.SetSlot(_hoveredSlotIndex, hotbarItem);
                return;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Shift+Click quick transfer
        // ────────────────────────────────────────────────────────────────────

        private void ShiftClickInventorySlot(int slotIndex)
        {
            ItemStack slotItem = _inventory.GetSlot(slotIndex);

            if (slotItem.IsEmpty)
            {
                return;
            }

            ItemEntry def = _itemRegistry.Get(slotItem.ItemId);
            int maxStack = def != null ? def.MaxStackSize : 64;

            if (slotIndex < Inventory.HotbarSize)
            {
                // Hotbar -> main inventory (slots 9-35)
                int remaining = TransferToRange(slotItem, Inventory.HotbarSize, Inventory.SlotCount, maxStack);
                _inventory.SetSlot(slotIndex, remaining > 0
                    ? new ItemStack(slotItem.ItemId, remaining, slotItem.Durability)
                    : ItemStack.Empty);
            }
            else
            {
                // Main -> hotbar (slots 0-8)
                int remaining = TransferToRange(slotItem, 0, Inventory.HotbarSize, maxStack);
                _inventory.SetSlot(slotIndex, remaining > 0
                    ? new ItemStack(slotItem.ItemId, remaining, slotItem.Durability)
                    : ItemStack.Empty);
            }
        }

        private int TransferToRange(ItemStack source, int startSlot, int endSlot, int maxStack)
        {
            int remaining = source.Count;

            // First pass: fill existing stacks of the same item
            for (int i = startSlot; i < endSlot && remaining > 0; i++)
            {
                ItemStack slot = _inventory.GetSlot(i);

                if (slot.IsEmpty || slot.ItemId != source.ItemId)
                {
                    continue;
                }

                int space = maxStack - slot.Count;

                if (space <= 0)
                {
                    continue;
                }

                int toMove = Mathf.Min(remaining, space);
                ItemStack updated = slot;
                updated.Count += toMove;
                _inventory.SetSlot(i, updated);
                remaining -= toMove;
            }

            // Second pass: empty slots
            for (int i = startSlot; i < endSlot && remaining > 0; i++)
            {
                if (!_inventory.GetSlot(i).IsEmpty)
                {
                    continue;
                }

                int toMove = Mathf.Min(remaining, maxStack);
                _inventory.SetSlot(i, new ItemStack(source.ItemId, toMove, source.Durability));
                remaining -= toMove;
            }

            return remaining;
        }

        // ────────────────────────────────────────────────────────────────────
        // Open / Close
        // ────────────────────────────────────────────────────────────────────

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

            // Stop any active paint
            _isPainting = false;
            _paintPending = false;
            _paintedSlots.Clear();
            _hoveredSlotIndex = -1;

            // Return held item to inventory (only clear if it fit)
            if (!_heldItem.IsEmpty)
            {
                ItemEntry def = _itemRegistry.Get(_heldItem.ItemId);
                int maxStack = def != null ? def.MaxStackSize : 64;

                if (_heldItem.Durability > 0)
                {
                    int leftOver = _inventory.AddItemWithDurability(_heldItem.ItemId, _heldItem.Durability);
                    _heldItem = leftOver == 0 ? ItemStack.Empty : _heldItem;
                }
                else
                {
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
            }

            // Return crafting grid items to inventory (full stacks, not just 1)
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    ItemStack gridStack = _craftingGrid.GetSlotStack(x, y);

                    if (!gridStack.IsEmpty)
                    {
                        ItemEntry def = _itemRegistry.Get(gridStack.ItemId);
                        int maxStack = def != null ? def.MaxStackSize : 64;

                        if (gridStack.Durability > 0)
                        {
                            int leftOver = _inventory.AddItemWithDurability(gridStack.ItemId, gridStack.Durability);

                            if (leftOver == 0)
                            {
                                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
                            }
                        }
                        else
                        {
                            int leftOver = _inventory.AddItem(gridStack.ItemId, gridStack.Count, maxStack);

                            if (leftOver == 0)
                            {
                                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
                            }
                            else
                            {
                                ItemStack partial = gridStack;
                                partial.Count = leftOver;
                                _craftingGrid.SetSlotStack(x, y, partial);
                            }
                        }
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Display helpers
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes a single inventory slot's visual content only if the item changed.
        /// Avoids rebuilding VisualElements — just updates text labels.
        /// </summary>
        private void RefreshSlot(int index)
        {
            if (_invSlotElements[index] == null)
            {
                return;
            }

            ItemStack stack = _inventory.GetSlot(index);

            if (stack.ItemId == _lastSlotState[index].ItemId &&
                stack.Count == _lastSlotState[index].Count &&
                stack.Durability == _lastSlotState[index].Durability)
            {
                return;
            }

            _lastSlotState[index] = stack;
            UpdateSlotDisplay(_invNameLabels[index], _invCountLabels[index], stack);
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
