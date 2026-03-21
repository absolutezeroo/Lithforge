using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Item.Interaction;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.UI.Container;
using Lithforge.Voxel.Item;

using UnityEngine;

namespace Lithforge.Runtime.UI.Interaction
{
    /// <summary>
    ///     Screen-agnostic controller for all slot interaction modes:
    ///     1. Left-click:   pick up / place / merge / swap
    ///     2. Right-click:  pick up half / place 1
    ///     3. Shift+click:  quick transfer between hotbar and main
    ///     4. Paint-drag:   right-click drag to distribute 1 item per slot
    ///     5. Number keys:  swap hovered slot with hotbar slot
    ///     6. Output take:  consume craft ingredients and collect result
    ///     Operates entirely on ISlotContainer — no knowledge of specific screen layout.
    ///     Slot mutations are delegated to <see cref="SlotActionExecutor" /> (Tier 2);
    ///     this controller manages UI concerns (animation, held stack, paint state).
    /// </summary>
    public sealed class SlotInteractionController
    {
        /// <summary>Item registry for looking up max stack sizes during slot interactions.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Set of composite keys (container hash + slot index) tracking slots already painted this drag.</summary>
        private readonly HashSet<long> _paintedSlots = new();

        /// <summary>Tool material registry for calculating repair kit repair amounts.</summary>
        private readonly ToolMaterialRegistry _toolMaterialRegistry;

        /// <summary>Tool template registry for resolving tool data during shift-click output crafting.</summary>
        private readonly ToolTemplateRegistry _toolTemplateRegistry;

        /// <summary>True when the user is actively dragging to distribute one item per slot.</summary>
        private bool _isPainting;

        /// <summary>Container where the paint-drag originated.</summary>
        private ISlotContainer _paintOriginContainer;

        /// <summary>Slot index where the paint-drag originated.</summary>
        private int _paintOriginSlot;

        /// <summary>True between the initial right-click place and the first hover on a different slot.</summary>
        private bool _paintPending;

        /// <summary>
        ///     Optional callback invoked after each optimistic slot click for network sync.
        ///     Parameters: slotIndex, clickType, button, container.
        /// </summary>
        public Action<int, byte, byte, ISlotContainer> OnSlotClicked;

        /// <summary>Creates a slot interaction controller with the required registries for item lookups and repair.</summary>
        public SlotInteractionController(
            HeldStack held,
            ItemRegistry itemRegistry,
            ToolTemplateRegistry toolTemplateRegistry,
            ToolMaterialRegistry toolMaterialRegistry)
        {
            Held = held;
            _itemRegistry = itemRegistry;
            _toolTemplateRegistry = toolTemplateRegistry;
            _toolMaterialRegistry = toolMaterialRegistry;
        }

        /// <summary>The held stack currently attached to the cursor.</summary>
        public HeldStack Held { get; }

        /// <summary>Index of the slot currently under the cursor, or -1 if none.</summary>
        public int HoveredSlotIndex { get; private set; } = -1;

        /// <summary>Container of the slot currently under the cursor, or null if none.</summary>
        public ISlotContainer HoveredContainer { get; private set; }

        /// <summary>Handles pointer entering a slot: updates hover state and continues paint-drag if active.</summary>
        public void OnSlotEnter(ISlotContainer container, int slotIndex)
        {
            HoveredContainer = container;
            HoveredSlotIndex = slotIndex;

            HandlePaintHover(container, slotIndex);
        }

        /// <summary>Handles pointer leaving a slot: clears hover state if it matches the departing slot.</summary>
        public void OnSlotLeave(ISlotContainer container, int slotIndex)
        {
            if (HoveredContainer == container && HoveredSlotIndex == slotIndex)
            {
                HoveredContainer = null;
                HoveredSlotIndex = -1;
            }
        }

        /// <summary>Handles left-click on a slot: delegates mutation to SlotActionExecutor.</summary>
        public void LeftClick(ISlotContainer container, int slotIndex)
        {
            if (container.IsReadOnly)
            {
                return;
            }

            ItemStack cursor = Held.IsEmpty ? ItemStack.Empty : Held.Stack;
            SlotActionResult result = SlotActionExecutor.Execute(
                container, ref cursor, slotIndex, SlotActionExecutor.ClickLeft, 0, _itemRegistry, 0);

            if (result.Outcome is not SlotActionOutcome.Success)
            {
                return;
            }

            Held.Set(cursor);
            container.OnSlotChanged(slotIndex);
            OnSlotClicked?.Invoke(slotIndex, 0, 0, container);
        }

        /// <summary>Handles right-click on a slot: repair-kit application, then delegates mutation to SlotActionExecutor.</summary>
        public void RightClick(ISlotContainer container, int slotIndex)
        {
            if (container.IsReadOnly)
            {
                return;
            }

            ItemStack slotItem = container.GetSlot(slotIndex);

            // Repair kit on tool: TiC-style inventory repair (controller-specific UI concern)
            if (!Held.IsEmpty && !slotItem.IsEmpty && _toolMaterialRegistry is not null)
            {
                ToolPartDataComponent kitComp = Held.Stack.Components?.Get<ToolPartDataComponent>(
                    DataComponentTypes.ToolPartDataId);

                if (kitComp is
                    {
                        PartData:
                        {
                            PartType: ToolPartType.RepairKit,
                        },
                    })
                {
                    ToolInstanceComponent toolComp = slotItem.Components?.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);

                    if (toolComp is not null)
                    {
                        ToolInstance tool = toolComp.Tool;
                        int repairAmount = RepairKitHelper.CalculateRepairKitRepair(
                            tool, kitComp.PartData, _toolMaterialRegistry);

                        if (repairAmount > 0)
                        {
                            tool.SetCurrentDurability(
                                (tool.IsBroken ? 0 : tool.CurrentDurability) + repairAmount);

                            ItemStack repairedStack = slotItem;
                            repairedStack.Durability = tool.CurrentDurability;
                            DataComponentMap repairedMap = new();
                            repairedMap.Set(DataComponentTypes.ToolInstanceId,
                                new ToolInstanceComponent(tool));
                            repairedStack.Components = repairedMap;
                            container.SetSlot(slotIndex, repairedStack);

                            ItemStack newHeld = Held.Stack;
                            newHeld.Count -= 1;
                            Held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                            container.OnSlotChanged(slotIndex);

                            return;
                        }
                    }
                }
            }

            bool hadCursor = !Held.IsEmpty;
            ItemStack cursor = Held.IsEmpty ? ItemStack.Empty : Held.Stack;
            SlotActionResult result = SlotActionExecutor.Execute(
                container, ref cursor, slotIndex, SlotActionExecutor.ClickRight, 0, _itemRegistry, 0);

            if (result.CursorChanged)
            {
                Held.Set(cursor);
            }

            // Start paint-drag after placing 1 item (cursor was held, still has items, and a slot was modified)
            if (hadCursor && !Held.IsEmpty && result is
                {
                    Outcome: SlotActionOutcome.Success,
                    ChangedSlotCount: > 0
                })
            {
                StartPaint(container, slotIndex);
            }

            container.OnSlotChanged(slotIndex);
            OnSlotClicked?.Invoke(slotIndex, 1, 0, container);
        }

        /// <summary>
        ///     Quick-transfers a slot between hotbar and main inventory via SlotActionExecutor.
        /// </summary>
        public void ShiftClick(
            ISlotContainer source,
            int slotIndex,
            InventoryContainerAdapter hotbarAdapter,
            InventoryContainerAdapter mainAdapter)
        {
            ItemStack slotItem = source.GetSlot(slotIndex);

            if (slotItem.IsEmpty)
            {
                return;
            }

            // Get the underlying inventory and compute absolute index
            Inventory inventory = hotbarAdapter.Inventory;
            int absoluteIndex;

            if (source == hotbarAdapter)
            {
                absoluteIndex = hotbarAdapter.ToAbsoluteIndex(slotIndex);
            }
            else
            {
                absoluteIndex = mainAdapter.ToAbsoluteIndex(slotIndex);
            }

            ItemStack cursor = Held.IsEmpty ? ItemStack.Empty : Held.Stack;
            SlotActionResult result = SlotActionExecutor.Execute(
                inventory, ref cursor, absoluteIndex, SlotActionExecutor.ClickShiftLeft, 0,
                _itemRegistry, Inventory.HotbarSize);

            if (result.CursorChanged)
            {
                Held.Set(cursor);
            }

            source.OnSlotChanged(slotIndex);
            OnSlotClicked?.Invoke(slotIndex, 2, 0, source);
        }

        /// <summary>
        ///     Swaps the hovered inventory slot with the given hotbar slot index via SlotActionExecutor.
        /// </summary>
        public void NumberKeySwap(
            ISlotContainer hoveredContainer,
            int hoveredIndex,
            InventoryContainerAdapter hotbarAdapter,
            int hotbarSlot)
        {
            if (hoveredContainer is null || hoveredIndex < 0)
            {
                return;
            }

            // Don't swap with self
            if (hoveredContainer == hotbarAdapter && hoveredIndex == hotbarSlot)
            {
                return;
            }

            // Delegate to SlotActionExecutor when hovering over an inventory adapter
            if (hoveredContainer is InventoryContainerAdapter adapter)
            {
                Inventory inventory = hotbarAdapter.Inventory;
                int absoluteIndex = adapter.ToAbsoluteIndex(hoveredIndex);

                ItemStack cursor = Held.IsEmpty ? ItemStack.Empty : Held.Stack;
                SlotActionExecutor.Execute(
                    inventory, ref cursor, absoluteIndex, SlotActionExecutor.ClickNumberKey,
                    (byte)hotbarSlot, _itemRegistry, Inventory.HotbarSize);

                hotbarAdapter.OnSlotChanged(hotbarSlot);
                hoveredContainer.OnSlotChanged(hoveredIndex);
                OnSlotClicked?.Invoke(hoveredIndex, 4, (byte)hotbarSlot, hoveredContainer);
                return;
            }

            // Cross-container swap (e.g. crafting grid → hotbar): direct swap
            ItemStack hotbarItem = hotbarAdapter.GetSlot(hotbarSlot);
            ItemStack hoveredItem = hoveredContainer.GetSlot(hoveredIndex);

            hotbarAdapter.SetSlot(hotbarSlot, hoveredItem);
            hoveredContainer.SetSlot(hoveredIndex, hotbarItem);
            hotbarAdapter.OnSlotChanged(hotbarSlot);
            hoveredContainer.OnSlotChanged(hoveredIndex);
            OnSlotClicked?.Invoke(hoveredIndex, 4, (byte)hotbarSlot, hoveredContainer);
        }

        /// <summary>
        ///     Takes the crafting output. Adds result to held stack.
        /// </summary>
        public void TakeOutput(CraftingOutputContainerAdapter output, CraftingGridContainerAdapter grid)
        {
            if (output.CurrentMatch is null)
            {
                return;
            }

            ItemStack result = output.GetSlot(0);

            if (Held.IsEmpty)
            {
                Held.Set(result);
            }
            else if (ItemStack.CanStack(Held.Stack, result))
            {
                int maxStack = GetMaxStack(Held.Stack);

                if (Held.Stack.Count + result.Count <= maxStack)
                {
                    ItemStack updated = Held.Stack;
                    updated.Count += result.Count;
                    Held.Set(updated);
                }
                else
                {
                    return; // Can't fit
                }
            }
            else
            {
                return; // Different item
            }

            output.TakeOutput(grid);
            OnSlotClicked?.Invoke(0, 5, 0, output);
        }

        /// <summary>
        ///     Shift+click on output: craft as many as possible and send to inventory.
        /// </summary>
        public void ShiftClickOutput(
            CraftingOutputContainerAdapter output,
            CraftingGridContainerAdapter grid,
            Inventory inventory)
        {
            for (int i = 0; i < 64; i++)
            {
                if (output.CurrentMatch is null)
                {
                    break;
                }

                RecipeEntry match = output.CurrentMatch;
                ItemEntry resultDef = _itemRegistry.Get(match.ResultItem);
                int maxStack = resultDef?.MaxStackSize ?? 64;

                byte[] toolData = _toolTemplateRegistry?.GetTemplate(match.ResultItem);

                if (toolData is not null)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(toolData);
                    int durability = tool?.MaxDurability ?? -1;
                    ItemStack resultStack = new(match.ResultItem, 1, durability);
                    DataComponentMap toolMap = new();
                    toolMap.Set(DataComponentTypes.ToolInstanceId,
                        new ToolInstanceComponent(tool));
                    resultStack.Components = toolMap;
                    int leftOver = inventory.AddItemStack(resultStack);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }
                else
                {
                    int leftOver = inventory.AddItem(match.ResultItem, match.ResultCount, maxStack);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }

                output.TakeOutput(grid);
            }
        }

        /// <summary>Handles pointer-up: ends paint-drag mode when right mouse button is released.</summary>
        public void OnPointerUp(int button)
        {
            if (button == 1)
            {
                _isPainting = false;
                _paintPending = false;
                _paintedSlots.Clear();
            }
        }

        /// <summary>
        ///     Returns held items to inventory on screen close.
        /// </summary>
        public void ReturnHeldToInventory(Inventory inventory)
        {
            if (Held.IsEmpty)
            {
                return;
            }

            ItemStack heldStack = Held.Stack;
            ItemEntry def = _itemRegistry.Get(heldStack.ItemId);
            int maxStack = def?.MaxStackSize ?? 64;

            if (heldStack.HasComponents)
            {
                int leftOver = inventory.AddItemStack(heldStack);
                Held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else if (heldStack.Durability > 0)
            {
                int leftOver = inventory.AddItemWithDurability(heldStack.ItemId, heldStack.Durability);
                Held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else
            {
                int leftOver = inventory.AddItem(heldStack.ItemId, heldStack.Count, maxStack);

                if (leftOver == 0)
                {
                    Held.Clear();
                }
                else
                {
                    ItemStack partial = heldStack;
                    partial.Count = leftOver;
                    Held.Set(partial);
                }
            }
        }

        /// <summary>
        ///     Resets paint/hover state. Call on screen close.
        /// </summary>
        public void ResetState()
        {
            _isPainting = false;
            _paintPending = false;
            _paintedSlots.Clear();
            HoveredContainer = null;
            HoveredSlotIndex = -1;
        }

        /// <summary>Initializes paint-drag state: captures origin slot and container.</summary>
        private void StartPaint(ISlotContainer container, int slotIndex)
        {
            _isPainting = false;
            _paintPending = true;
            _paintOriginSlot = slotIndex;
            _paintOriginContainer = container;
            _paintedSlots.Clear();
            _paintedSlots.Add(MakePaintKey(container, slotIndex));
        }

        /// <summary>Delegates a single paint-slot placement to SlotActionExecutor during an active paint-drag.</summary>
        private void HandlePaintHover(ISlotContainer container, int slotIndex)
        {
            // Activate pending paint when pointer enters a different slot than origin
            if (_paintPending && (container != _paintOriginContainer || slotIndex != _paintOriginSlot))
            {
                _isPainting = true;
                _paintPending = false;
            }

            if (!_isPainting || slotIndex < 0)
            {
                return;
            }

            if (container.IsReadOnly)
            {
                return;
            }

            long key = MakePaintKey(container, slotIndex);

            if (_paintedSlots.Contains(key))
            {
                return;
            }

            if (Held.IsEmpty)
            {
                _isPainting = false;
                _paintPending = false;
                _paintedSlots.Clear();
                return;
            }

            ItemStack cursor = Held.Stack;
            SlotActionResult result = SlotActionExecutor.ExecutePaintSlot(
                container, ref cursor, slotIndex, _itemRegistry);

            if (result.Outcome is not SlotActionOutcome.Success || result.ChangedSlotCount == 0)
            {
                return;
            }

            Held.Set(cursor);
            _paintedSlots.Add(key);
            container.OnSlotChanged(slotIndex);
        }

        /// <summary>Returns the maximum stack size for the given item, defaulting to 64 if unknown.</summary>
        private int GetMaxStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return 64;
            }

            ItemEntry def = _itemRegistry.Get(stack.ItemId);
            return def?.MaxStackSize ?? 64;
        }

        /// <summary>Generates a unique 64-bit key from a container hash and slot index for paint-drag tracking.</summary>
        private static long MakePaintKey(ISlotContainer container, int slotIndex)
        {
            return (long)container.GetHashCode() << 32 | (uint)slotIndex;
        }
    }
}
