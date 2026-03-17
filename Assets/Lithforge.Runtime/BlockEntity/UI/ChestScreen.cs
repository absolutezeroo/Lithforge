using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Voxel.Item;

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
        private BlockEntityContainerAdapter _chestAdapter;
        private ChestBlockEntity _currentChest;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

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

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }

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
            ChestBlockEntity chest = entity as ChestBlockEntity;

            if (chest == null)
            {
                return;
            }

            _currentChest = chest;
            _chestAdapter = new BlockEntityContainerAdapter(chest.Inventory);

            RebuildUI();
            Open();
        }

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

        protected override void OnClose()
        {
            // Return held items to player inventory
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);
            _currentChest = null;
        }
    }
}
