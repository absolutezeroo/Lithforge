using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
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
        private CraftingGrid _craftingGrid;

        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;
        private CraftingGridContainerAdapter _craftAdapter;
        private CraftingOutputContainerAdapter _outputAdapter;

        private static readonly Key[] s_numberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        public void Initialize(ScreenContext context)
        {
            _craftingGrid = new CraftingGrid(2, 2);

            _outputAdapter = new CraftingOutputContainerAdapter(context.ItemRegistry, context.ToolTemplateRegistry);
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);
            _craftAdapter = new CraftingGridContainerAdapter(
                _craftingGrid, context.CraftingEngine, _outputAdapter);

            InitializeBase(context, 200, "UI/Screens/PlayerInventoryScreen");

            BuildUI();
        }

        private void BuildUI()
        {
            if (!CloneTemplate())
            {
                return;
            }

            VisualElement craftGrid = QueryContainer("craft-grid");
            VisualElement outputSlot = QueryContainer("output-slot");
            VisualElement mainSlots = QueryContainer("main-slots");
            VisualElement hotbarSlots = QueryContainer("hotbar-slots");

            if (craftGrid == null || outputSlot == null || mainSlots == null || hotbarSlots == null)
            {
                return;
            }

            SlotGroupDefinition craftGroupDef = SlotGroupDefinition.Create("craft", 2, 2);
            BuildSlotGroup(craftGroupDef, _craftAdapter, craftGrid);

            BuildSingleSlot(_outputAdapter, 0, outputSlot);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, mainSlots);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, hotbarSlots);
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
                        Interaction.ShiftClickOutput(_outputAdapter, _craftAdapter, Context.PlayerInventory);
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
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);

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
                            int leftOver = Context.PlayerInventory.AddItemWithDurability(
                                gridStack.ItemId, gridStack.Durability);

                            if (leftOver == 0)
                            {
                                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
                            }
                        }
                        else
                        {
                            int leftOver = Context.PlayerInventory.AddItem(
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
            if (Context == null)
            {
                return;
            }

            // Escape closes inventory (before E key check)
            if (IsOpen && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Toggle with E key
            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                // If a block entity screen is open (or was just closed this frame
                // by its own Update running first), consume E without opening inventory
                if (Context.ScreenManager != null &&
                    (Context.ScreenManager.HasActiveScreen ||
                     Context.ScreenManager.WasClosedThisFrame))
                {
                    if (Context.ScreenManager.HasActiveScreen)
                    {
                        Context.ScreenManager.CloseActive();
                    }

                    return;
                }

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

            if (IsInGracePeriod())
            {
                RefreshAllSlots();
                return;
            }

            // Handle number keys 1-9 for hotbar swap
            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }

        private void HandleNumberKeys(Keyboard keyboard)
        {
            ISlotContainer hoveredContainer = Interaction.HoveredContainer;
            int hoveredIndex = Interaction.HoveredSlotIndex;

            if (hoveredContainer == null || hoveredIndex < 0)
            {
                return;
            }

            for (int i = 0; i < s_numberKeys.Length; i++)
            {
                if (!keyboard[s_numberKeys[i]].wasPressedThisFrame)
                {
                    continue;
                }

                Interaction.NumberKeySwap(hoveredContainer, hoveredIndex, _hotbarAdapter, i);
                return;
            }
        }

    }
}
