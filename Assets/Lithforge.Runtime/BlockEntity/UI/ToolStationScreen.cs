using Lithforge.Core.Data;
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

            // Output slot: take assembled tool
            if (container == _outputAdapter)
            {
                if (evt.button == 0)
                {
                    HandleOutputTake();
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

            // Serialize tool to CustomData
            byte[] toolData = ToolInstanceSerializer.Serialize(tool);

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
            resultStack.CustomData = toolData;

            bool isShift = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);

            if (isShift)
            {
                // Transfer to player inventory (preserves CustomData)
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

        private static ResourceId GetResultItemId(ToolInstance tool)
        {
            // Convention: result item is "lithforge:<material>_<tooltype>"
            // For now, use a generic modular tool item
            string toolName = tool.ToolType.ToString().ToLowerInvariant();

            if (tool.Parts != null && tool.Parts.Length > 0)
            {
                ResourceId headMat = tool.Parts[0].MaterialId;

                if (!string.IsNullOrEmpty(headMat.Name))
                {
                    return new ResourceId(headMat.Namespace, headMat.Name + "_" + toolName);
                }
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
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            // Update assembly preview only when slots change
            if (_currentStation != null && _previewDirty
                && _resultLabel != null && _statsLabel != null)
            {
                _previewDirty = false;
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
            }

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }
    }
}
