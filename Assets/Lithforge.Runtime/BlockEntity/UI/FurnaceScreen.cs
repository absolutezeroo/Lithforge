using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Item;
using UnityEngine;
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

        public void Initialize(ScreenContext context)
        {
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            InitializeBase(context, 250);

            Panel.style.display = DisplayStyle.None;
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
            ClearSlotBindings();
            Panel.Clear();

            // Re-add overlay class after clear
            Panel.AddToClassList("lf-overlay");

            // Container panel centered in overlay
            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Title
            Label title = new Label("Furnace");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Furnace section: input + progress + output in a row
            VisualElement furnaceSection = new VisualElement();
            furnaceSection.AddToClassList("lf-craft-section");
            container.Add(furnaceSection);

            // Input slot (slot 0)
            VisualElement inputCol = new VisualElement();
            Label inputLabel = new Label("Input");
            inputLabel.AddToClassList("lf-section-label");
            inputCol.Add(inputLabel);
            BuildSingleSlot(_furnaceInputAdapter, 0, inputCol);
            furnaceSection.Add(inputCol);

            // Fuel slot + burn bar
            VisualElement fuelCol = new VisualElement();
            Label fuelLabel = new Label("Fuel");
            fuelLabel.AddToClassList("lf-section-label");
            fuelCol.Add(fuelLabel);
            BuildSingleSlot(_furnaceFuelAdapter, 0, fuelCol);

            _burnBar = new ProgressBar();
            _burnBar.title = "Burn";
            _burnBar.style.width = 60;
            _burnBar.style.height = 16;
            fuelCol.Add(_burnBar);
            furnaceSection.Add(fuelCol);

            // Arrow + smelt progress
            VisualElement arrowCol = new VisualElement();
            arrowCol.style.justifyContent = Justify.Center;
            arrowCol.style.alignItems = Align.Center;

            _smeltBar = new ProgressBar();
            _smeltBar.title = "Smelt";
            _smeltBar.style.width = 80;
            _smeltBar.style.height = 16;
            arrowCol.Add(_smeltBar);

            Label arrow = new Label("=>");
            arrow.AddToClassList("lf-craft-arrow");
            arrowCol.Add(arrow);
            furnaceSection.Add(arrowCol);

            // Output slot (slot 2, read-only)
            VisualElement outputCol = new VisualElement();
            Label outputLabel = new Label("Output");
            outputLabel.AddToClassList("lf-section-label");
            outputCol.Add(outputLabel);
            BuildSingleSlot(_furnaceOutputAdapter, 0, outputCol);
            furnaceSection.Add(outputCol);

            // Separator
            VisualElement sep1 = new VisualElement();
            sep1.AddToClassList("lf-separator");
            container.Add(sep1);

            // Main inventory: 9x3
            Label mainLabel = new Label("Inventory");
            mainLabel.AddToClassList("lf-section-label");
            container.Add(mainLabel);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, container);

            // Separator
            VisualElement sep2 = new VisualElement();
            sep2.AddToClassList("lf-separator");
            sep2.style.marginTop = 8;
            container.Add(sep2);

            // Hotbar: 9x1
            Label hotbarLabel = new Label("Hotbar");
            hotbarLabel.AddToClassList("lf-section-label");
            container.Add(hotbarLabel);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, container);
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
                            else if (Interaction.Held.Stack.ItemId == outputStack.ItemId)
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

            // Escape to close
            if (IsOpen && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (!IsOpen)
            {
                return;
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
    }
}
