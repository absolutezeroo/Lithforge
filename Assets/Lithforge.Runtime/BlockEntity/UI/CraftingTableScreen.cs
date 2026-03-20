using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.BlockEntity.UI
{
    /// <summary>
    ///     Screen for the crafting table block entity. Shows a 3x3 crafting grid,
    ///     output slot, player inventory and hotbar. Escape key closes.
    ///     The grid is local to the screen — no persistent state on the entity.
    /// </summary>
    public sealed class CraftingTableScreen : ContainerScreen
    {
        /// <summary>Keyboard digit keys used for number-key slot swap shortcuts.</summary>
        private static readonly Key[] s_numberKeys =
        {
            Key.Digit1,
            Key.Digit2,
            Key.Digit3,
            Key.Digit4,
            Key.Digit5,
            Key.Digit6,
            Key.Digit7,
            Key.Digit8,
            Key.Digit9,
        };

        /// <summary>Container adapter wrapping the 3x3 crafting grid slots.</summary>
        private CraftingGridContainerAdapter _craftAdapter;

        /// <summary>Local 3x3 crafting grid holding transient slot state during the session.</summary>
        private CraftingGrid _craftingGrid;

        /// <summary>Container adapter wrapping the player hotbar slots.</summary>
        private InventoryContainerAdapter _hotbarAdapter;

        /// <summary>Container adapter wrapping the player main inventory slots.</summary>
        private InventoryContainerAdapter _mainAdapter;

        /// <summary>Container adapter for the crafting output slot with recipe match display.</summary>
        private CraftingOutputContainerAdapter _outputAdapter;

        /// <summary>Polls keyboard input for close keys and number-key shortcuts, refreshes slot display.</summary>
        private void Update()
        {
            if (Context == null)
            {
                return;
            }

            if (IsOpen && Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                 Keyboard.current[Context.KeyBindings?.Inventory ?? Key.E].wasPressedThisFrame))
            {
                Close();
                return;
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

            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }

        /// <summary>Creates the crafting grid and adapters, then loads the UXML template.</summary>
        public void Initialize(ScreenContext context)
        {
            _craftingGrid = new CraftingGrid(3, 3);

            _outputAdapter = new CraftingOutputContainerAdapter(context.ItemRegistry, context.ToolTemplateRegistry);
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);
            _craftAdapter = new CraftingGridContainerAdapter(
                _craftingGrid, context.CraftingEngine, _outputAdapter);

            InitializeBase(context, 255, "UI/Screens/CraftingTableScreen");
        }

        /// <summary>Opens the crafting table screen with a fresh local grid, ignoring entity state.</summary>
        public void OpenForEntity(BlockEntity entity)
        {
            // The crafting table entity carries no persistent crafting state.
            // We just open with a fresh local grid.
            RebuildUI();
            Open();
        }

        /// <summary>Clones the UXML template and binds crafting grid, output, main inventory, and hotbar slots.</summary>
        private void RebuildUI()
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

            SlotGroupDefinition craftGroupDef = SlotGroupDefinition.Create("craft", 3, 3);
            BuildSlotGroup(craftGroupDef, _craftAdapter, craftGrid);

            BuildSingleSlot(_outputAdapter, 0, outputSlot);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, mainSlots);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, hotbarSlots);
        }

        /// <summary>Handles pointer-down on slots: output take, shift-click transfers, and regular click interactions.</summary>
        protected override void OnSlotPointerDown(
            ISlotContainer container, int slotIndex, PointerDownEvent evt)
        {
            if (!IsOpen)
            {
                return;
            }

            // Output slot: take crafted item
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
                if (container == _craftAdapter)
                {
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _mainAdapter, _hotbarAdapter, ItemRegistryRef);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _craftAdapter, null, ItemRegistryRef);
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

        /// <summary>Returns held items and remaining grid contents to the player inventory on close.</summary>
        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);

            // Return crafting grid items to player inventory
            for (int y = 0; y < _craftingGrid.Height; y++)
            {
                for (int x = 0; x < _craftingGrid.Width; x++)
                {
                    ItemStack gridStack = _craftingGrid.GetSlotStack(x, y);

                    if (!gridStack.IsEmpty)
                    {
                        ItemEntry def = ItemRegistryRef.Get(gridStack.ItemId);
                        int maxStack = def?.MaxStackSize ?? 64;

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

        /// <summary>Checks digit keys 1-9 and swaps the hovered slot with the corresponding hotbar slot.</summary>
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
