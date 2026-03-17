using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Voxel.Item;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.BlockEntity.UI
{
    /// <summary>
    /// Screen for the furnace block entity. Shows input slot, fuel slot,
    /// output slot, burn/smelt progress bars, and player inventory below.
    /// Escape key closes.
    /// </summary>
    public sealed class FurnaceScreen : ContainerScreen
    {
        private FurnaceBlockEntity _currentFurnace;

        private BlockEntityContainerAdapter _furnaceInputAdapter;
        private BlockEntityContainerAdapter _furnaceFuelAdapter;
        private BlockEntityContainerAdapter _furnaceOutputAdapter;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

        private ProgressBar _burnBar;
        private ProgressBar _smeltBar;

        private static readonly Key[] s_numberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        public void Initialize(ScreenContext context)
        {
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            InitializeBase(context, 250, "UI/Screens/FurnaceScreen");
        }

        /// <summary>
        /// Opens the furnace screen for the given entity.
        /// Creates adapters wrapping the 3 furnace slots:
        /// input=slot 0, fuel=slot 1, output=slot 2 (read-only for placement).
        /// </summary>
        public void OpenForEntity(BlockEntity entity)
        {
            FurnaceBlockEntity furnace = entity as FurnaceBlockEntity;

            if (furnace == null)
            {
                return;
            }

            _currentFurnace = furnace;

            InventoryBehavior inv = furnace.Inventory;

            // Create single-slot adapters for input, fuel, output.
            // Each adapter exposes exactly 1 slot to prevent cross-slot corruption
            // during shift-click transfers via TryFillContainer.
            _furnaceInputAdapter = new BlockEntityContainerAdapter(
                inv, FurnaceBlockEntity.InputSlot, 1, false);
            _furnaceFuelAdapter = new BlockEntityContainerAdapter(
                inv, FurnaceBlockEntity.FuelSlot, 1, false);
            _furnaceOutputAdapter = new BlockEntityContainerAdapter(
                inv, FurnaceBlockEntity.OutputSlot, 1, true);

            RebuildUI();
            Open();
        }

        private void RebuildUI()
        {
            _burnBar = null;
            _smeltBar = null;

            if (!CloneTemplate())
            {
                return;
            }

            VisualElement inputSlot = QueryContainer("input-slot");
            VisualElement fuelSlot = QueryContainer("fuel-slot");
            VisualElement outputSlot = QueryContainer("output-slot");
            VisualElement mainSlots = QueryContainer("main-slots");
            VisualElement hotbarSlots = QueryContainer("hotbar-slots");

            _burnBar = Panel.Q<ProgressBar>("burn-bar");
            _smeltBar = Panel.Q<ProgressBar>("smelt-bar");

            if (inputSlot == null || fuelSlot == null || outputSlot == null
                || mainSlots == null || hotbarSlots == null
                || _burnBar == null || _smeltBar == null)
            {
                return;
            }

            BuildSingleSlot(_furnaceInputAdapter, 0, inputSlot);
            BuildSingleSlot(_furnaceFuelAdapter, 0, fuelSlot);
            BuildSingleSlot(_furnaceOutputAdapter, 0, outputSlot);

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

            // Output slot: take-only (read-only adapter, so handle manually)
            if (container == _furnaceOutputAdapter)
            {
                if (evt.button == 0)
                {
                    bool isShift = Keyboard.current != null &&
                        (Keyboard.current.leftShiftKey.isPressed ||
                         Keyboard.current.rightShiftKey.isPressed);

                    ItemStack outputStack = container.GetSlot(slotIndex);

                    if (!outputStack.IsEmpty)
                    {
                        if (isShift)
                        {
                            // Transfer output to player inventory
                            ItemEntry def = ItemRegistryRef.Get(outputStack.ItemId);
                            int maxStack = def != null ? def.MaxStackSize : 64;
                            int leftOver = Context.PlayerInventory.AddItem(
                                outputStack.ItemId, outputStack.Count, maxStack);

                            if (leftOver == 0)
                            {
                                _currentFurnace.Inventory.SetSlot(
                                    FurnaceBlockEntity.OutputSlot, ItemStack.Empty);
                            }
                            else
                            {
                                ItemStack partial = outputStack;
                                partial.Count = leftOver;
                                _currentFurnace.Inventory.SetSlot(
                                    FurnaceBlockEntity.OutputSlot, partial);
                            }
                        }
                        else
                        {
                            // Pick up output to cursor (bypass read-only adapter)
                            if (Interaction.Held.IsEmpty)
                            {
                                Interaction.Held.Set(outputStack);
                                _currentFurnace.Inventory.SetSlot(
                                    FurnaceBlockEntity.OutputSlot, ItemStack.Empty);
                            }
                            else if (ItemStack.CanStack(Interaction.Held.Stack, outputStack))
                            {
                                // Merge into held stack
                                ItemEntry def = ItemRegistryRef.Get(outputStack.ItemId);
                                int maxStack = def != null ? def.MaxStackSize : 64;
                                int space = maxStack - Interaction.Held.Stack.Count;
                                int toTake = outputStack.Count < space
                                    ? outputStack.Count : space;

                                if (toTake > 0)
                                {
                                    ItemStack newHeld = Interaction.Held.Stack;
                                    newHeld.Count += toTake;
                                    Interaction.Held.Set(newHeld);

                                    if (outputStack.Count - toTake <= 0)
                                    {
                                        _currentFurnace.Inventory.SetSlot(
                                            FurnaceBlockEntity.OutputSlot, ItemStack.Empty);
                                    }
                                    else
                                    {
                                        ItemStack remain = outputStack;
                                        remain.Count -= toTake;
                                        _currentFurnace.Inventory.SetSlot(
                                            FurnaceBlockEntity.OutputSlot, remain);
                                    }
                                }
                            }
                        }
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
                if (container == _furnaceInputAdapter || container == _furnaceFuelAdapter)
                {
                    // From furnace -> player main, then hotbar
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _mainAdapter, _hotbarAdapter, ItemRegistryRef);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    // From player -> furnace input slot
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _furnaceInputAdapter, _furnaceFuelAdapter,
                        ItemRegistryRef);
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
            // Return held items to player inventory
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);
            _currentFurnace = null;
        }

        private void Update()
        {
            if (Context == null)
            {
                return;
            }

            // Escape or E to close
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

            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            // Update progress bars
            if (_currentFurnace != null)
            {
                if (_burnBar != null)
                {
                    _burnBar.value = _currentFurnace.FuelBurn.BurnProgress * 100f;
                }

                if (_smeltBar != null)
                {
                    _smeltBar.value = _currentFurnace.Smelting.SmeltProgress * 100f;
                }
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
