using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
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
    /// Screen for the chest block entity. Shows 9x3 chest grid above
    /// 9x3 main inventory and 9x1 hotbar. Escape key closes.
    /// </summary>
    public sealed class ChestScreen : ContainerScreen
    {
        private Inventory _playerInventory;
        private ChestBlockEntity _currentChest;

        private BlockEntityContainerAdapter _chestAdapter;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

        public void Initialize(
            Inventory playerInventory,
            ItemRegistry itemRegistry,
            PanelSettings panelSettings,
            ItemSpriteAtlas spriteAtlas)
        {
            _playerInventory = playerInventory;

            _hotbarAdapter = new InventoryContainerAdapter(playerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(playerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            HeldStack held = new HeldStack();
            SlotInteractionController interaction = new SlotInteractionController(held, itemRegistry);

            InitializeBase(panelSettings, 250, interaction, spriteAtlas, itemRegistry);

            Panel.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Opens the chest screen for the given chest entity.
        /// Rebuilds slot bindings each time a different chest is opened.
        /// </summary>
        public void OpenForEntity(ChestBlockEntity chest)
        {
            _currentChest = chest;
            _chestAdapter = new BlockEntityContainerAdapter(chest.Inventory);

            RebuildUI();
            Open();
        }

        private void RebuildUI()
        {
            Panel.Clear();

            // Re-add overlay class after clear
            Panel.AddToClassList("lf-overlay");

            // Container panel centered in overlay
            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Re-wire panel events
            Panel.RegisterCallback<PointerUpEvent>(evt =>
            {
                Interaction.OnPointerUp(evt.button);
            });

            Panel.RegisterCallback<PointerMoveEvent>(evt =>
            {
                // Stub — tooltip and ghost handled by base
            });

            // Title
            Label title = new Label("Chest");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Chest grid: 9x3 = 27 slots
            SlotGroupDefinition chestGroupDef = SlotGroupDefinition.Create("chest", 9, 3);
            BuildSlotGroup(chestGroupDef, _chestAdapter, container);

            // Separator
            VisualElement sep1 = new VisualElement();
            sep1.AddToClassList("lf-separator");
            container.Add(sep1);

            // Main inventory: 9x3 = 27 slots
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

            // Shift+left-click: quick transfer between chest and player
            if (evt.button == 0 && Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed))
            {
                if (container == _chestAdapter)
                {
                    // From chest → player main inventory first, then hotbar
                    TransferItem(container, slotIndex, _mainAdapter, _hotbarAdapter);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    // From player → chest
                    TransferItem(container, slotIndex, _chestAdapter, null);
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

            // Try primary target first
            remaining = TryFillContainer(stack.ItemId, remaining, maxStack, primaryTarget);

            // Then secondary if any remains
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
            Lithforge.Core.Data.ResourceId itemId, int count, int maxStack, ISlotContainer target)
        {
            int remaining = count;

            // Merge into existing stacks first
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

            // Fill empty slots
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
            // Return held items to player inventory
            Interaction.ReturnHeldToInventory(_playerInventory);
            _currentChest = null;
        }

        private void Update()
        {
            if (_playerInventory == null)
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

            RefreshAllSlots();
        }
    }
}
