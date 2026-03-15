using Lithforge.Core.Data;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using UnityEngine;
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
        private Inventory _playerInventory;
        private CraftingGrid _craftingGrid;

        private CraftingGridContainerAdapter _craftAdapter;
        private CraftingOutputContainerAdapter _outputAdapter;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

        public void Initialize(
            Inventory playerInventory,
            ItemRegistry itemRegistry,
            CraftingEngine craftingEngine,
            PanelSettings panelSettings,
            ItemSpriteAtlas spriteAtlas)
        {
            _playerInventory = playerInventory;
            _craftingGrid = new CraftingGrid(3, 3);

            _outputAdapter = new CraftingOutputContainerAdapter(itemRegistry);
            _hotbarAdapter = new InventoryContainerAdapter(playerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(playerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);
            _craftAdapter = new CraftingGridContainerAdapter(_craftingGrid, craftingEngine, _outputAdapter);

            HeldStack held = new HeldStack();
            SlotInteractionController interaction = new SlotInteractionController(held, itemRegistry);

            InitializeBase(panelSettings, 255, interaction, spriteAtlas, itemRegistry);

            Panel.style.display = DisplayStyle.None;
        }

        public void OpenForEntity(CraftingTableBlockEntity entity)
        {
            RebuildUI();
            Open();
        }

        private void RebuildUI()
        {
            Panel.Clear();
            Panel.AddToClassList("lf-overlay");

            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Title
            Label title = new Label("Crafting");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Crafting section: 3x3 grid + arrow + output
            VisualElement craftSection = new VisualElement();
            craftSection.AddToClassList("lf-craft-section");
            container.Add(craftSection);

            SlotGroupDefinition craftGroupDef = SlotGroupDefinition.Create("craft", 3, 3);
            BuildSlotGroup(craftGroupDef, _craftAdapter, craftSection);

            Label arrow = new Label("=>");
            arrow.AddToClassList("lf-craft-arrow");
            craftSection.Add(arrow);

            BuildSingleSlot(_outputAdapter, 0, craftSection);

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
                        Interaction.ShiftClickOutput(_outputAdapter, _craftAdapter, _playerInventory);
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
                    TransferItem(container, slotIndex, _mainAdapter, _hotbarAdapter);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    TransferItem(container, slotIndex, _craftAdapter, null);
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

        private void TransferItem(
            ISlotContainer source, int slotIndex,
            ISlotContainer primaryTarget, ISlotContainer secondaryTarget)
        {
            ItemStack stack = source.GetSlot(slotIndex);

            if (stack.IsEmpty)
            {
                return;
            }

            ItemEntry def = ItemRegistryRef.Get(stack.ItemId);
            int maxStack = def != null ? def.MaxStackSize : 64;
            int remaining = stack.Count;

            remaining = TryFillContainer(stack.ItemId, remaining, maxStack, primaryTarget);

            if (remaining > 0 && secondaryTarget != null)
            {
                remaining = TryFillContainer(stack.ItemId, remaining, maxStack, secondaryTarget);
            }

            if (remaining == 0)
            {
                source.SetSlot(slotIndex, ItemStack.Empty);
            }
            else
            {
                ItemStack updated = stack;
                updated.Count = remaining;
                source.SetSlot(slotIndex, updated);
            }
        }

        private static int TryFillContainer(
            ResourceId itemId, int count, int maxStack, ISlotContainer target)
        {
            int remaining = count;

            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                ItemStack slot = target.GetSlot(i);

                if (!slot.IsEmpty && slot.ItemId == itemId && slot.Count < maxStack)
                {
                    int space = maxStack - slot.Count;
                    int toAdd = remaining < space ? remaining : space;
                    ItemStack updated = slot;
                    updated.Count += toAdd;
                    target.SetSlot(i, updated);
                    remaining -= toAdd;
                }
            }

            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                if (target.GetSlot(i).IsEmpty)
                {
                    int toAdd = remaining < maxStack ? remaining : maxStack;
                    target.SetSlot(i, new ItemStack(itemId, toAdd));
                    remaining -= toAdd;
                }
            }

            return remaining;
        }

        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(_playerInventory);

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
                            int leftOver = _playerInventory.AddItemWithDurability(
                                gridStack.ItemId, gridStack.Durability);

                            if (leftOver == 0)
                            {
                                _craftingGrid.SetSlotStack(x, y, ItemStack.Empty);
                            }
                        }
                        else
                        {
                            int leftOver = _playerInventory.AddItem(
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
            if (_playerInventory == null)
            {
                return;
            }

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

            RefreshAllSlots();
        }
    }
}
