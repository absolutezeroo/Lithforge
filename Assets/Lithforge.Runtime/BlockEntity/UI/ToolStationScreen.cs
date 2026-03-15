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

            InitializeBase(context, 260);

            Panel.style.display = DisplayStyle.None;
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
            ClearSlotBindings();
            Panel.Clear();
            Panel.AddToClassList("lf-overlay");

            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // PointerUpEvent is already handled by ContainerScreen.InitializeBase

            // Title
            Label title = new Label("Tool Station");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Tool type selector row
            VisualElement selectorRow = new VisualElement();
            selectorRow.AddToClassList("lf-slot-row");
            selectorRow.style.marginBottom = 8;
            container.Add(selectorRow);

            _pickaxeBtn = CreateToolTypeButton("Pickaxe", ToolType.Pickaxe, selectorRow);
            _axeBtn = CreateToolTypeButton("Axe", ToolType.Axe, selectorRow);
            _shovelBtn = CreateToolTypeButton("Shovel", ToolType.Shovel, selectorRow);
            _swordBtn = CreateToolTypeButton("Sword", ToolType.Sword, selectorRow);

            UpdateToolTypeButtonStyles();

            // Part slots section
            VisualElement partsSection = new VisualElement();
            partsSection.AddToClassList("lf-craft-section");
            container.Add(partsSection);

            // Head slot
            VisualElement headCol = new VisualElement();
            Label headLabel = new Label("Head");
            headLabel.AddToClassList("lf-section-label");
            headCol.Add(headLabel);
            BuildSingleSlot(_headAdapter, 0, headCol);
            partsSection.Add(headCol);

            // Handle slot
            VisualElement handleCol = new VisualElement();
            Label handleLabel = new Label("Handle");
            handleLabel.AddToClassList("lf-section-label");
            handleCol.Add(handleLabel);
            BuildSingleSlot(_handleAdapter, 0, handleCol);
            partsSection.Add(handleCol);

            // Binding slot
            VisualElement bindingCol = new VisualElement();
            Label bindingLabel = new Label("Binding");
            bindingLabel.AddToClassList("lf-section-label");
            bindingCol.Add(bindingLabel);
            BuildSingleSlot(_bindingAdapter, 0, bindingCol);
            partsSection.Add(bindingCol);

            // Arrow
            VisualElement arrowCol = new VisualElement();
            arrowCol.style.justifyContent = Justify.Center;
            arrowCol.style.alignItems = Align.Center;
            Label arrow = new Label("=>");
            arrow.AddToClassList("lf-craft-arrow");
            arrowCol.Add(arrow);
            partsSection.Add(arrowCol);

            // Output slot
            VisualElement outputCol = new VisualElement();
            Label outputLabel = new Label("Result");
            outputLabel.AddToClassList("lf-section-label");
            outputCol.Add(outputLabel);
            BuildSingleSlot(_outputAdapter, 0, outputCol);
            partsSection.Add(outputCol);

            // Stats preview
            _resultLabel = new Label("");
            _resultLabel.AddToClassList("lf-section-label");
            _resultLabel.style.marginTop = 4;
            container.Add(_resultLabel);

            _statsLabel = new Label("");
            _statsLabel.AddToClassList("lf-section-label");
            _statsLabel.style.fontSize = 10;
            container.Add(_statsLabel);

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

        private Button CreateToolTypeButton(string label, ToolType toolType, VisualElement parent)
        {
            Button btn = new Button();
            btn.text = label;
            btn.style.width = 70;
            btn.style.height = 24;
            btn.style.marginRight = 4;

            ToolType capturedType = toolType;
            btn.clicked += () =>
            {
                _selectedToolType = capturedType;
                _previewDirty = true;
                UpdateToolTypeButtonStyles();
            };

            parent.Add(btn);
            return btn;
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
            if (selected)
            {
                btn.style.backgroundColor = new Color(0.3f, 0.5f, 0.3f, 1f);
                btn.style.color = Color.white;
            }
            else
            {
                btn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                btn.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
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

            ItemStack resultStack = new ItemStack(resultItemId, 1);
            resultStack.Durability = tool.MaxDurability;
            resultStack.CustomData = toolData;

            bool isShift = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);

            if (isShift)
            {
                // Transfer to player inventory
                ItemEntry def = ItemRegistryRef.Get(resultItemId);
                int maxStack = def != null ? def.MaxStackSize : 1;
                int leftOver = Context.PlayerInventory.AddItem(resultItemId, 1, maxStack);

                if (leftOver == 0)
                {
                    // TODO: CustomData not preserved by AddItem — needs ItemStack-level AddItem
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
            if (_currentStation != null && _previewDirty)
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
        }
    }
}
