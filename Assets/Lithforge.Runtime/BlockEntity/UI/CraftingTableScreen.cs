using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.BlockEntity.UI
{
    /// <summary>
    /// Screen for the crafting table block entity. Shows a 3x3 crafting grid,
    /// output slot, player inventory and hotbar. Escape key closes.
    /// The grid is local to the screen — no persistent state on the entity.
    /// </summary>
    public sealed class CraftingTableScreen : ContainerScreen
    {
        private CraftingGrid _craftingGrid;

        private CraftingGridContainerAdapter _craftAdapter;
        private CraftingOutputContainerAdapter _outputAdapter;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

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

        public void OpenForEntity(BlockEntity entity)
        {
            // The crafting table entity carries no persistent crafting state.
            // We just open with a fresh local grid.
            RebuildUI();
            Open();
        }

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

            if (IsOpen && Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                 Keyboard.current.eKey.wasPressedThisFrame))
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

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }
    }
}
