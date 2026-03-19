using System;

using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Navigation;

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Player inventory screen with 36 inventory slots (9 hotbar + 27 main),
    ///     2x2 crafting grid, and output slot. Opened with E key.
    /// </summary>
    public sealed class PlayerInventoryScreen : ContainerScreen, IScreen
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

        /// <summary>Container adapter wrapping the 2x2 crafting grid slots.</summary>
        private CraftingGridContainerAdapter _craftAdapter;

        /// <summary>Local 2x2 crafting grid holding transient slot state during the session.</summary>
        private CraftingGrid _craftingGrid;

        /// <summary>Container adapter wrapping the player hotbar slots (indices 0-8).</summary>
        private InventoryContainerAdapter _hotbarAdapter;

        /// <summary>Container adapter wrapping the player main inventory slots (indices 9-35).</summary>
        private InventoryContainerAdapter _mainAdapter;

        /// <summary>Container adapter for the crafting output slot with recipe match display.</summary>
        private CraftingOutputContainerAdapter _outputAdapter;

        /// <summary>Handles E-key toggle, number-key slot swaps, and refreshes slot display each frame.</summary>
        private void Update()
        {
            if (Context == null)
            {
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

        /// <summary>Returns the screen name identifier for the player inventory.</summary>
        public string ScreenName { get { return ScreenNames.PlayerInventory; } }

        /// <summary>Returns true because the inventory screen consumes all input when open.</summary>
        public bool IsInputOpaque { get { return true; } }

        /// <summary>Returns true because the inventory screen requires a visible cursor.</summary>
        public bool RequiresCursor { get { return true; } }

        /// <summary>Shows the inventory screen when pushed onto the navigation stack.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            SetVisible(true);
        }

        /// <summary>Closes the inventory if open and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (IsOpen)
            {
                Close();
            }

            onComplete();
        }

        /// <summary>Closes the inventory on Escape if open and returns true to consume the key event.</summary>
        public bool HandleEscape()
        {
            if (IsOpen)
            {
                Close();
                return true;
            }

            return false;
        }

        /// <summary>Creates the 2x2 crafting grid, adapters, loads the UXML template, and builds the slot layout.</summary>
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

        /// <summary>Clones the UXML template and binds the 2x2 crafting grid, output, main inventory, and hotbar slots.</summary>
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

        /// <summary>Handles pointer-down: output take with shift-click crafting, shift-click transfers, and regular clicks.</summary>
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

        /// <summary>Returns held items and remaining crafting grid items to the player inventory on close.</summary>
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
