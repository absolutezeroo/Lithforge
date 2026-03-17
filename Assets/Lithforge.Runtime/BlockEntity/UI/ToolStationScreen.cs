using Lithforge.Core.Data;
using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
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
    /// Screen for the tool station block entity. Shows 3 part slots (head, handle, binding),
    /// tool type selector buttons, result preview label, and output slot.
    /// Player inventory and hotbar below.
    /// </summary>
    public sealed class ToolStationScreen : ContainerScreen
    {
        private ToolStationBlockEntity _currentStation;
        private ToolTraitRegistry _traitRegistry;

        private BlockEntityContainerAdapter _headAdapter;
        private BlockEntityContainerAdapter _handleAdapter;
        private BlockEntityContainerAdapter _bindingAdapter;
        private BlockEntityContainerAdapter _outputAdapter;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;

        private ToolType _selectedToolType = ToolType.Pickaxe;
        private Label _resultLabel;
        private Label _statsLabel;
        private ToolInstance _cachedPreview;
        private bool _previewDirty = true;

        // Tool type buttons for styling
        private Button _pickaxeBtn;
        private Button _axeBtn;
        private Button _shovelBtn;
        private Button _swordBtn;
        private Label _modeLabel;

        // Repair mode state
        private int[] _repairItemsConsumed;
        private int _repairTotalRepair;

        private static readonly Key[] s_numberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        public void Initialize(ScreenContext context)
        {
            _traitRegistry = context.ToolTraitRegistry;

            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            InitializeBase(context, 260, "UI/Screens/ToolStationScreen");
        }

        public void OpenForEntity(BlockEntity entity)
        {
            ToolStationBlockEntity station = entity as ToolStationBlockEntity;

            if (station == null)
            {
                return;
            }

            _currentStation = station;

            InventoryBehavior inv = station.Inventory;

            _headAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.HeadSlot, 1, false);
            _handleAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.HandleSlot, 1, false);
            _bindingAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.BindingSlot, 1, false);
            _outputAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.OutputSlot, 1, true);

            RebuildUI();
            Open();
        }

        private void RebuildUI()
        {
            _resultLabel = null;
            _statsLabel = null;
            _modeLabel = null;
            _pickaxeBtn = null;
            _axeBtn = null;
            _shovelBtn = null;
            _swordBtn = null;

            if (!CloneTemplate())
            {
                return;
            }

            VisualElement headSlot = QueryContainer("head-slot");
            VisualElement handleSlot = QueryContainer("handle-slot");
            VisualElement bindingSlot = QueryContainer("binding-slot");
            VisualElement outputSlot = QueryContainer("output-slot");
            VisualElement mainSlots = QueryContainer("main-slots");
            VisualElement hotbarSlots = QueryContainer("hotbar-slots");

            _resultLabel = Panel.Q<Label>("result-label");
            _statsLabel = Panel.Q<Label>("stats-label");
            _modeLabel = Panel.Q<Label>("mode-label");

            _pickaxeBtn = Panel.Q<Button>("pickaxe-btn");
            _axeBtn = Panel.Q<Button>("axe-btn");
            _shovelBtn = Panel.Q<Button>("shovel-btn");
            _swordBtn = Panel.Q<Button>("sword-btn");

            if (headSlot == null || handleSlot == null || bindingSlot == null
                || outputSlot == null || mainSlots == null || hotbarSlots == null
                || _resultLabel == null || _statsLabel == null
                || _pickaxeBtn == null || _axeBtn == null
                || _shovelBtn == null || _swordBtn == null)
            {
                return;
            }

            // Wire button click handlers
            _pickaxeBtn.clicked += () =>
            {
                _selectedToolType = ToolType.Pickaxe;
                _previewDirty = true;
                UpdateToolTypeButtonStyles();
            };
            _axeBtn.clicked += () =>
            {
                _selectedToolType = ToolType.Axe;
                _previewDirty = true;
                UpdateToolTypeButtonStyles();
            };
            _shovelBtn.clicked += () =>
            {
                _selectedToolType = ToolType.Shovel;
                _previewDirty = true;
                UpdateToolTypeButtonStyles();
            };
            _swordBtn.clicked += () =>
            {
                _selectedToolType = ToolType.Sword;
                _previewDirty = true;
                UpdateToolTypeButtonStyles();
            };

            UpdateToolTypeButtonStyles();

            BuildSingleSlot(_headAdapter, 0, headSlot);
            BuildSingleSlot(_handleAdapter, 0, handleSlot);
            BuildSingleSlot(_bindingAdapter, 0, bindingSlot);
            BuildSingleSlot(_outputAdapter, 0, outputSlot);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, mainSlots);

            SlotGroupDefinition hotbarGroupDef = SlotGroupDefinition.Create("hotbar", 9, 1);
            BuildSlotGroup(hotbarGroupDef, _hotbarAdapter, hotbarSlots);
        }

        private void UpdateToolTypeButtonStyles()
        {
            SetButtonSelected(_pickaxeBtn, _selectedToolType == ToolType.Pickaxe);
            SetButtonSelected(_axeBtn, _selectedToolType == ToolType.Axe);
            SetButtonSelected(_shovelBtn, _selectedToolType == ToolType.Shovel);
            SetButtonSelected(_swordBtn, _selectedToolType == ToolType.Sword);
        }

        private static void SetButtonSelected(Button btn, bool selected)
        {
            if (btn == null)
            {
                return;
            }

            if (selected)
            {
                btn.AddToClassList("lf-btn--selected");
            }
            else
            {
                btn.RemoveFromClassList("lf-btn--selected");
            }
        }

        protected override void OnSlotPointerDown(
            ISlotContainer container, int slotIndex, PointerDownEvent evt)
        {
            if (!IsOpen)
            {
                return;
            }

            // Any slot interaction may change part slots
            _previewDirty = true;

            // Output slot: take assembled tool or repaired tool
            if (container == _outputAdapter)
            {
                if (evt.button == 0)
                {
                    if (IsRepairMode())
                    {
                        HandleRepairOutputTake();
                    }
                    else
                    {
                        HandleOutputTake();
                    }
                }

                evt.StopPropagation();
                return;
            }

            // Shift+left-click transfer
            if (evt.button == 0 && Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed))
            {
                if (container == _headAdapter || container == _handleAdapter
                    || container == _bindingAdapter)
                {
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _mainAdapter, _hotbarAdapter, ItemRegistryRef);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    // Try to fill part slots in order: head, handle, binding
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _headAdapter, _handleAdapter, ItemRegistryRef);
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

        private void HandleOutputTake()
        {
            if (_currentStation == null)
            {
                return;
            }

            // Try to assemble
            ToolInstance tool = _currentStation.Assembly.TryAssemble(_selectedToolType);

            if (tool == null)
            {
                return;
            }

            // Determine the result item ID based on tool type and head material
            ResourceId resultItemId = GetResultItemId(tool);

            // Generate composite sprite for the modular tool (always re-composite
            // since the same resultItemId may map to different part material combos,
            // and legacy tool compositing may have registered a head-only sprite)
            if (Context.ToolPartTextures != null)
            {
                Sprite composite = ToolSpriteCompositor.Composite(
                    tool, Context.ToolPartTextures);

                if (composite != null)
                {
                    Context.ItemSpriteAtlas.Register(resultItemId, composite);
                }
            }

            ItemStack resultStack = new ItemStack(resultItemId, 1);
            resultStack.Durability = tool.MaxDurability;
            DataComponentMap toolMap = new DataComponentMap();
            toolMap.Set(DataComponentTypes.ToolInstanceId, new ToolInstanceComponent(tool));
            resultStack.Components = toolMap;

            bool isShift = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);

            if (isShift)
            {
                // Transfer to player inventory (preserves Components)
                int leftOver = Context.PlayerInventory.AddItemStack(resultStack);

                if (leftOver == 0)
                {
                    _currentStation.Assembly.ConsumeInputParts();
                }
            }
            else if (Interaction.Held.IsEmpty)
            {
                Interaction.Held.Set(resultStack);
                _currentStation.Assembly.ConsumeInputParts();
            }
        }

        private bool IsRepairMode()
        {
            ItemStack headSlot = _headAdapter.GetSlot(0);

            if (headSlot.IsEmpty || !headSlot.HasComponents)
            {
                return false;
            }

            return headSlot.Components.Has(DataComponentTypes.ToolInstanceId);
        }

        private void UpdateRepairPreview()
        {
            ItemStack toolStack = _headAdapter.GetSlot(0);

            ToolInstanceComponent headToolComp = toolStack.Components?.Get<ToolInstanceComponent>(
                DataComponentTypes.ToolInstanceId);

            if (headToolComp == null)
            {
                _outputAdapter.SetSlot(0, ItemStack.Empty);
                _repairItemsConsumed = null;
                _repairTotalRepair = 0;
                return;
            }

            ToolInstance tool = headToolComp.Tool;
            int damageToRepair = tool.IsBroken
                ? tool.MaxDurability
                : tool.MaxDurability - tool.CurrentDurability;

            if (damageToRepair <= 0)
            {
                _outputAdapter.SetSlot(0, ItemStack.Empty);
                _repairItemsConsumed = null;
                _repairTotalRepair = 0;

                if (_resultLabel != null && _statsLabel != null)
                {
                    _resultLabel.text = "Tool is at full durability";
                    _statsLabel.text = "";
                }

                return;
            }

            ResourceId headMaterial = RepairKitHelper.GetHeadMaterial(tool);

            if (headMaterial.Namespace == null)
            {
                _outputAdapter.SetSlot(0, ItemStack.Empty);
                _repairItemsConsumed = null;
                _repairTotalRepair = 0;
                return;
            }

            int totalRepair = 0;
            int[] itemsConsumed = new int[2];

            for (int i = 0; i < 2; i++)
            {
                ISlotContainer adapter = i == 0
                    ? (ISlotContainer)_handleAdapter
                    : _bindingAdapter;

                ItemStack stack = adapter.GetSlot(0);

                if (stack.IsEmpty)
                {
                    continue;
                }

                int repairPerItem = 0;

                ToolPartDataComponent kitComp = stack.Components?.Get<ToolPartDataComponent>(
                    DataComponentTypes.ToolPartDataId);

                if (kitComp != null &&
                    kitComp.PartData.PartType == ToolPartType.RepairKit &&
                    kitComp.PartData.MaterialId.Equals(headMaterial))
                {
                    ToolMaterialData matData = Context.ToolMaterialRegistry != null
                        ? Context.ToolMaterialRegistry.Get(headMaterial)
                        : null;

                    if (matData != null)
                    {
                        repairPerItem = (int)(matData.HeadDurability *
                            RepairKitHelper.RepairKitValue / RepairKitHelper.UnitsPerRepair);
                    }
                }
                else if (Context.MaterialInputRegistry != null)
                {
                    MaterialInputData inputData = Context.MaterialInputRegistry.Get(stack.ItemId);

                    if (inputData != null && inputData.MaterialId.Equals(headMaterial))
                    {
                        repairPerItem = RepairKitHelper.CalculateRawMaterialRepair(
                            tool, inputData, Context.ToolMaterialRegistry);
                    }
                }

                if (repairPerItem <= 0)
                {
                    continue;
                }

                int remaining = damageToRepair - totalRepair;

                if (remaining <= 0)
                {
                    break;
                }

                int needed = (int)System.Math.Ceiling((float)remaining / repairPerItem);
                int used = System.Math.Min(needed, stack.Count);
                itemsConsumed[i] = used;
                totalRepair += used * repairPerItem;
            }

            totalRepair = System.Math.Min(totalRepair, damageToRepair);

            if (totalRepair <= 0)
            {
                _outputAdapter.SetSlot(0, ItemStack.Empty);
                _repairItemsConsumed = null;
                _repairTotalRepair = 0;

                if (_resultLabel != null && _statsLabel != null)
                {
                    _resultLabel.text = "Add matching repair material";
                    _statsLabel.text = "";
                }

                return;
            }

            ToolInstance repaired = headToolComp.Tool.Clone();
            int baseCurrentDur = repaired.IsBroken ? 0 : repaired.CurrentDurability;
            repaired.SetCurrentDurability(baseCurrentDur + totalRepair);

            ItemStack previewStack = toolStack;
            previewStack.Durability = repaired.CurrentDurability;
            DataComponentMap repairedMap = new DataComponentMap();
            repairedMap.Set(DataComponentTypes.ToolInstanceId,
                new ToolInstanceComponent(repaired));
            previewStack.Components = repairedMap;

            _outputAdapter.SetSlot(0, previewStack);

            _repairItemsConsumed = itemsConsumed;
            _repairTotalRepair = totalRepair;

            if (_resultLabel != null)
            {
                _resultLabel.text = "Repair +" + totalRepair + " durability";
                _statsLabel.text = repaired.CurrentDurability + " / " + repaired.MaxDurability;
            }
        }

        private void HandleRepairOutputTake()
        {
            if (_repairItemsConsumed == null || _repairTotalRepair <= 0)
            {
                return;
            }

            ItemStack outputStack = _outputAdapter.GetSlot(0);

            if (outputStack.IsEmpty)
            {
                return;
            }

            bool isShift = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);

            if (isShift)
            {
                int leftOver = Context.PlayerInventory.AddItemStack(outputStack);

                if (leftOver > 0)
                {
                    return;
                }
            }
            else if (Interaction.Held.IsEmpty)
            {
                Interaction.Held.Set(outputStack);
            }
            else
            {
                return;
            }

            // Consume repair materials
            for (int i = 0; i < 2; i++)
            {
                if (_repairItemsConsumed[i] <= 0)
                {
                    continue;
                }

                ISlotContainer adapter = i == 0
                    ? (ISlotContainer)_handleAdapter
                    : _bindingAdapter;

                ItemStack slot = adapter.GetSlot(0);
                slot.Count -= _repairItemsConsumed[i];
                adapter.SetSlot(0, slot.Count <= 0 ? ItemStack.Empty : slot);
            }

            // Remove tool from input
            _headAdapter.SetSlot(0, ItemStack.Empty);
            _outputAdapter.SetSlot(0, ItemStack.Empty);

            _repairItemsConsumed = null;
            _repairTotalRepair = 0;
            _previewDirty = true;
        }

        private static ResourceId GetResultItemId(ToolInstance tool)
        {
            // Convention: result item is "lithforge:<material>_<tooltype>"
            string toolName = tool.ToolType.ToString().ToLowerInvariant();
            ResourceId headMat = RepairKitHelper.GetHeadMaterial(tool);

            if (!string.IsNullOrEmpty(headMat.Name))
            {
                return new ResourceId(headMat.Namespace, headMat.Name + "_" + toolName);
            }

            return new ResourceId("lithforge", "modular_" + toolName);
        }

        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);
            _currentStation = null;
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

            if (Keyboard.current != null)
            {
                HandleNumberKeys(Keyboard.current);
            }

            // Update preview only when slots change
            if (_currentStation != null && _previewDirty
                && _resultLabel != null && _statsLabel != null)
            {
                _previewDirty = false;

                if (IsRepairMode())
                {
                    UpdateRepairPreview();

                    if (_modeLabel != null)
                    {
                        _modeLabel.text = "Repair";
                    }
                }
                else
                {
                    _cachedPreview = _currentStation.Assembly.TryAssemble(_selectedToolType);

                    if (_cachedPreview != null)
                    {
                        _resultLabel.text = _selectedToolType.ToString() + " (Lvl " + _cachedPreview.EffectiveToolLevel + ")";
                        _statsLabel.text = "Speed: " + _cachedPreview.BaseSpeed.ToString("F1")
                            + "  Durability: " + _cachedPreview.MaxDurability
                            + "  Damage: " + _cachedPreview.BaseDamage.ToString("F1");
                    }
                    else
                    {
                        _resultLabel.text = "Place parts to assemble";
                        _statsLabel.text = "";
                    }

                    if (_modeLabel != null)
                    {
                        _modeLabel.text = "Assembly";
                    }
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
