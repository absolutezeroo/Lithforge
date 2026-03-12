using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Player inventory screen with 36 inventory slots (9 hotbar + 27 main),
    /// 2x2 crafting grid, and output slot. Opened with E key.
    /// </summary>
    public sealed class PlayerInventoryScreen : ContainerScreen
    {
        private Inventory _inventory;
        private CraftingGrid _craftingGrid;

        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;
        private CraftingGridContainerAdapter _craftAdapter;
        private CraftingOutputContainerAdapter _outputAdapter;

        private static readonly Key[] _numberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        public void Initialize(
            Inventory inventory,
            ItemRegistry itemRegistry,
            CraftingEngine craftingEngine,
            PanelSettings panelSettings,
            ItemSpriteAtlas spriteAtlas)
        {
            _inventory = inventory;
            _craftingGrid = new CraftingGrid(2, 2);

            // Build container adapters
            _outputAdapter = new CraftingOutputContainerAdapter(itemRegistry);
            _hotbarAdapter = new InventoryContainerAdapter(inventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(inventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);
            _craftAdapter = new CraftingGridContainerAdapter(_craftingGrid, craftingEngine, _outputAdapter);

            // Build interaction controller
            HeldStack held = new HeldStack();
            SlotInteractionController interaction = new SlotInteractionController(held, itemRegistry);

            InitializeBase(panelSettings, 200, interaction, spriteAtlas, itemRegistry);

            BuildUI();
            Panel.style.display = DisplayStyle.None;
        }

        private void BuildUI()
        {
            // Container panel centered in overlay
            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Title
            Label title = new Label("Inventory");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Crafting section
            VisualElement craftSection = new VisualElement();
            craftSection.AddToClassList("lf-craft-section");
            container.Add(craftSection);

            // 2x2 crafting grid
            SlotGroupDefinition craftGroupDef = SlotGroupDefinition.Create("craft", 2, 2);
            BuildSlotGroup(craftGroupDef, _craftAdapter, craftSection);

            // Arrow
            Label arrow = new Label("=>");
            arrow.AddToClassList("lf-craft-arrow");
            craftSection.Add(arrow);

            // Output slot
            BuildSingleSlot(_outputAdapter, 0, craftSection);

            // Separator
            VisualElement sep1 = new VisualElement();
            sep1.AddToClassList("lf-separator");
            container.Add(sep1);

            // Main inventory label + grid (27 slots in 3 rows of 9)
            Label mainLabel = new Label("Main");
            mainLabel.AddToClassList("lf-section-label");
            container.Add(mainLabel);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, container);

            // Separator
            VisualElement sep2 = new VisualElement();
            sep2.AddToClassList("lf-separator");
            sep2.style.marginTop = 8;
            container.Add(sep2);

            // Hotbar label + row (9 slots)
            Label hotbarLabel = new Label("Hotbar");
            hotbarLabel.AddToClassList("lf-section-label");
            container.Add(hotbarLabel);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, container);
        }

        protected override void OnSlotPointerDown(ISlotContainer container, int slotIndex, PointerDownEvent evt)
        {
            if (!IsOpen)
            {
                return;
            }

            // Output slot: special handling
            if (container == _outputAdapter)
            {
                if (evt.button == 0)
                {
                    bool isShift = Keyboard.current != null &&
                        (Keyboard.current.leftShiftKey.isPressed ||
                         Keyboard.current.rightShiftKey.isPressed);

                    if (isShift)
                    {
                        Interaction.ShiftClickOutput(_outputAdapter, _craftAdapter, _inventory);
                    }
                    else
                    {
                        Interaction.TakeOutput(_outputAdapter, _craftAdapter);
                    }
                }

                evt.StopPropagation();
                return;
            }

            // Shift+left-click: quick transfer
            if (evt.button == 0 && Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed))
            {
                // Determine which adapter this container belongs to for transfer direction
                if (container == _hotbarAdapter || container == _mainAdapter)
                {
                    Interaction.ShiftClick(container, slotIndex, _hotbarAdapter, _mainAdapter);
                }

                evt.StopPropagation();
                return;
            }

            // Regular clicks
            if (evt.button == 0)
            {
                Interaction.LeftClick(container, slotIndex);
            }
            else if (evt.button == 1)
            {
                Interaction.RightClick(container, slotIndex);
            }

            evt.StopPropagation();
        }

        protected override void OnClose()
        {
            // Return held items to inventory
            Interaction.ReturnHeldToInventory(_inventory);

            // Return crafting grid items to inventory
            for (int y = 0; y < _craftingGrid.Height; y++)
            {
                for (int x = 0; x < _craftingGrid.Width; x++)
                {
                    ItemStack gridStack = _craftingGrid.GetSlotStack(x, y);

                    if (!gridStack.IsEmpty)
                    {
                        ItemEntry def = ItemRegistryRef.Get(gridStack.ItemId);
                        int maxStack = def != null ? def.MaxStackSize : 64;

                        if (gridStack.Durability > 0)
                        {
                            int leftOver = _inventory.AddItemWithDurability(
                                gridStack.ItemId, gridStack.Durability);

                            if (leftOver == 0)
                            {
                                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
                            }
                        }
                        else
                        {
                            int leftOver = _inventory.AddItem(
                                gridStack.ItemId, gridStack.Count, maxStack);

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
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }

            if (!IsOpen)
            {
                return;
            }

            // Handle number keys 1-9 for hotbar swap
            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            RefreshAllSlots();
        }

        private void HandleNumberKeys(Keyboard keyboard)
        {
            ISlotContainer hoveredContainer = Interaction.HoveredContainer;
            int hoveredIndex = Interaction.HoveredSlotIndex;

            if (hoveredContainer == null || hoveredIndex < 0)
            {
                return;
            }

            for (int i = 0; i < _numberKeys.Length; i++)
            {
                if (!keyboard[_numberKeys[i]].wasPressedThisFrame)
                {
                    continue;
                }

                Interaction.NumberKeySwap(hoveredContainer, hoveredIndex, _hotbarAdapter, i);
                return;
            }
        }

    }
}
