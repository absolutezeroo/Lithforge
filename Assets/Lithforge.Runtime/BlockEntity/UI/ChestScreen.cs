using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Item;

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.BlockEntity.UI
{
    /// <summary>
    ///     Screen for the chest block entity. Shows 9x3 chest grid above
    ///     9x3 main inventory and 9x1 hotbar. Escape key closes.
    /// </summary>
    public sealed class ChestScreen : ContainerScreen
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

        /// <summary>Container adapter wrapping the chest entity's inventory slots.</summary>
        private BlockEntityContainerAdapter _chestAdapter;

        /// <summary>The chest block entity currently being displayed.</summary>
        private ChestBlockEntity _currentChest;

        /// <summary>Container adapter wrapping the player hotbar slots.</summary>
        private InventoryContainerAdapter _hotbarAdapter;

        /// <summary>Container adapter wrapping the player main inventory slots.</summary>
        private InventoryContainerAdapter _mainAdapter;

        /// <summary>Polls keyboard input for close keys and number-key shortcuts, refreshes slot display.</summary>
        private void Update()
        {
            if (Context == null)
            {
                return;
            }

            // Escape or E to close
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

        /// <summary>Initializes adapters for the player hotbar and main inventory, then loads the UXML template.</summary>
        public void Initialize(ScreenContext context)
        {
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            InitializeBase(context, 250, "UI/Screens/ChestScreen");
        }

        /// <summary>
        ///     Opens the chest screen for the given entity.
        ///     Accepts the abstract <see cref="BlockEntity" /> type and casts internally.
        ///     Rebuilds slot bindings each time a different chest is opened.
        /// </summary>
        public void OpenForEntity(BlockEntity entity)
        {

            if (entity is not ChestBlockEntity chest)
            {
                return;
            }

            _currentChest = chest;
            _chestAdapter = new BlockEntityContainerAdapter(chest.Inventory);

            RebuildUI();
            Open();
        }

        /// <summary>Clones the UXML template and binds chest, main inventory, and hotbar slot groups.</summary>
        private void RebuildUI()
        {
            if (!CloneTemplate())
            {
                return;
            }

            VisualElement chestSlots = QueryContainer("chest-slots");
            VisualElement mainSlots = QueryContainer("main-slots");
            VisualElement hotbarSlots = QueryContainer("hotbar-slots");

            if (chestSlots == null || mainSlots == null || hotbarSlots == null)
            {
                return;
            }

            SlotGroupDefinition chestGroupDef = SlotGroupDefinition.Create("chest", 9, 3);
            BuildSlotGroup(chestGroupDef, _chestAdapter, chestSlots);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, mainSlots);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, hotbarSlots);
        }

        /// <summary>Handles pointer-down on slots: shift-click transfers between chest and player, regular click picks up or places.</summary>
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
                    // From chest -> player main inventory first, then hotbar
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _mainAdapter, _hotbarAdapter, ItemRegistryRef);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    // From player -> chest
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _chestAdapter, null, ItemRegistryRef);
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

        /// <summary>Returns true if the container is the chest adapter (WindowId > 0).</summary>
        protected override bool IsContainerSlot(ISlotContainer container)
        {
            return container == _chestAdapter;
        }

        /// <summary>Returns held items to the player inventory and clears the chest reference on close.</summary>
        protected override void OnClose()
        {
            // Return held items to player inventory
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);
            _currentChest = null;
        }
    }
}
