using System;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Crafting;
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
    ///     Screen for the tool station block entity. Three-column layout: tool type icon sidebar,
    ///     part input slots with arrow and output, and a right-side info panel showing stats/traits.
    ///     Player inventory and hotbar below.
    /// </summary>
    public sealed class ToolStationScreen : ContainerScreen
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

        /// <summary>White color for standard stat values.</summary>
        private static readonly Color s_statWhite = new(1f, 1f, 1f, 1f);

        /// <summary>Green color for positive stat values (repair restores).</summary>
        private static readonly Color s_statGreen = new(0.4f, 0.85f, 0.4f, 1f);

        /// <summary>Yellow color for notable stat values (mining level).</summary>
        private static readonly Color s_statYellow = new(1f, 0.85f, 0.3f, 1f);

        /// <summary>Red color for negative or missing stat values.</summary>
        private static readonly Color s_statRed = new(1f, 0.3f, 0.3f, 1f);

        /// <summary>Button for selecting the axe tool type.</summary>
        private Button _axeBtn;

        /// <summary>Icon element inside the axe button for sprite assignment.</summary>
        private VisualElement _axeIcon;

        /// <summary>Single-slot adapter wrapping the binding part slot (slot 2).</summary>
        private BlockEntityContainerAdapter _bindingAdapter;

        /// <summary>Cached tool instance from the most recent assembly preview computation.</summary>
        private ToolInstance _cachedPreview;

        /// <summary>The tool station block entity currently being displayed.</summary>
        private ToolStationBlockEntity _currentStation;

        /// <summary>Single-slot adapter wrapping the handle part slot (slot 1).</summary>
        private BlockEntityContainerAdapter _handleAdapter;

        /// <summary>Single-slot adapter wrapping the head part slot (slot 0).</summary>
        private BlockEntityContainerAdapter _headAdapter;

        /// <summary>Container adapter wrapping the player hotbar slots.</summary>
        private InventoryContainerAdapter _hotbarAdapter;

        /// <summary>Container adapter wrapping the player main inventory slots.</summary>
        private InventoryContainerAdapter _mainAdapter;

        /// <summary>Label displaying the current mode (Assembly or Repair).</summary>
        private Label _modeLabel;

        /// <summary>Single-slot read-only adapter wrapping the assembled tool output slot.</summary>
        private BlockEntityContainerAdapter _outputAdapter;

        /// <summary>Button for selecting the pickaxe tool type.</summary>
        private Button _pickaxeBtn;

        /// <summary>Icon element inside the pickaxe button for sprite assignment.</summary>
        private VisualElement _pickaxeIcon;

        /// <summary>Flag indicating the assembly preview needs recomputation due to slot changes.</summary>
        private bool _previewDirty = true;

        /// <summary>Preview image element showing the composite tool sprite.</summary>
        private VisualElement _previewImage;

        /// <summary>Per-slot count of repair material items consumed in the current repair preview.</summary>
        private int[] _repairItemsConsumed;

        /// <summary>Total durability points restored by the current repair preview.</summary>
        private int _repairTotalRepair;

        /// <summary>Label displaying the assembled or repaired tool name.</summary>
        private Label _resultLabel;

        /// <summary>Currently selected tool type for assembly (defaults to Pickaxe).</summary>
        private ToolType _selectedToolType = ToolType.Pickaxe;

        /// <summary>Button for selecting the shovel tool type.</summary>
        private Button _shovelBtn;

        /// <summary>Icon element inside the shovel button for sprite assignment.</summary>
        private VisualElement _shovelIcon;

        /// <summary>Container for per-line stat rows in the info panel.</summary>
        private VisualElement _statsContainer;

        /// <summary>Button for selecting the sword tool type.</summary>
        private Button _swordBtn;

        /// <summary>Icon element inside the sword button for sprite assignment.</summary>
        private VisualElement _swordIcon;

        /// <summary>Container for trait labels in the info panel.</summary>
        private VisualElement _traitsContainer;

        /// <summary>Registry for resolving tool trait descriptions in the UI.</summary>
        private ToolTraitRegistry _traitRegistry;

        /// <summary>Polls keyboard for close keys and number-key shortcuts, updates assembly/repair preview, refreshes slots.</summary>
        private void Update()
        {
            if (Context == null)
            {
                return;
            }

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

            // Update preview only when slots change
            if (_currentStation != null && _previewDirty
                && _resultLabel != null && _statsContainer != null)
            {
                _previewDirty = false;

                if (IsRepairMode())
                {
                    if (_modeLabel != null)
                    {
                        _modeLabel.text = "Repair";
                    }

                    // Get tool from head slot for info
                    ItemStack headStack = _headAdapter.GetSlot(0);
                    ToolInstanceComponent headComp = headStack.Components?.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);
                    ToolInstance headTool = headComp?.Tool;

                    int curDur = headTool != null
                        ? (headTool.IsBroken ? 0 : headTool.CurrentDurability)
                        : 0;
                    int maxDur = headTool?.MaxDurability ?? 0;

                    UpdateRepairPreview();
                    UpdateInfoPanelRepair(headTool, curDur, maxDur, _repairTotalRepair);
                }
                else
                {
                    if (_modeLabel != null)
                    {
                        _modeLabel.text = "Assembly";
                    }

                    _cachedPreview = _currentStation.Assembly.TryAssemble(_selectedToolType);
                    UpdateInfoPanelAssembly(_cachedPreview);
                }
            }

            RefreshAllSlots();
            UpdateTooltipKeyRefresh();
        }

        /// <summary>Stores the trait registry, creates player inventory adapters, and loads the UXML template.</summary>
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

        /// <summary>Opens the tool station screen for the given entity, creating single-slot adapters for each part slot.</summary>
        public void OpenForEntity(BlockEntity entity)
        {

            if (entity is not ToolStationBlockEntity station)
            {
                return;
            }

            _currentStation = station;

            InventoryBehavior inv = station.Inventory;

            _headAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.HeadSlot, 1);
            _handleAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.HandleSlot, 1);
            _bindingAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.BindingSlot, 1);
            _outputAdapter = new BlockEntityContainerAdapter(
                inv, ToolStationBlockEntity.OutputSlot, 1, true);

            RebuildUI();
            Open();
        }

        /// <summary>Clones the UXML template, queries info panel elements, wires tool type icon buttons, and binds all slot groups.</summary>
        private void RebuildUI()
        {
            _resultLabel = null;
            _modeLabel = null;
            _statsContainer = null;
            _traitsContainer = null;
            _previewImage = null;
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
            _modeLabel = Panel.Q<Label>("mode-label");
            _statsContainer = Panel.Q<VisualElement>("stats-container");
            _traitsContainer = Panel.Q<VisualElement>("traits-container");
            _previewImage = Panel.Q<VisualElement>("preview-image");

            _pickaxeBtn = Panel.Q<Button>("pickaxe-btn");
            _axeBtn = Panel.Q<Button>("axe-btn");
            _shovelBtn = Panel.Q<Button>("shovel-btn");
            _swordBtn = Panel.Q<Button>("sword-btn");

            // Locate icon children inside buttons
            _pickaxeIcon = Panel.Q<VisualElement>("pickaxe-icon");
            _axeIcon = Panel.Q<VisualElement>("axe-icon");
            _shovelIcon = Panel.Q<VisualElement>("shovel-icon");
            _swordIcon = Panel.Q<VisualElement>("sword-icon");

            if (headSlot == null || handleSlot == null || bindingSlot == null
                || outputSlot == null || mainSlots == null || hotbarSlots == null
                || _resultLabel == null || _statsContainer == null
                || _pickaxeBtn == null || _axeBtn == null
                || _shovelBtn == null || _swordBtn == null)
            {
                return;
            }

            // Assign tool type icon sprites from atlas
            AssignToolTypeIcons();

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

        /// <summary>Loads tool type icon sprites from Resources and assigns them to the sidebar button icon elements.</summary>
        private void AssignToolTypeIcons()
        {
            SetIconSprite(_pickaxeIcon, "UI/Sprites/ToolIcons/pickaxe_icon");
            SetIconSprite(_axeIcon, "UI/Sprites/ToolIcons/axe_icon");
            SetIconSprite(_shovelIcon, "UI/Sprites/ToolIcons/shovel_icon");
            SetIconSprite(_swordIcon, "UI/Sprites/ToolIcons/sword_icon");
        }

        /// <summary>Sets the background image of an icon element from a Resources sprite path.</summary>
        private static void SetIconSprite(VisualElement iconElement, string resourcePath)
        {
            if (iconElement == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);

            if (sprite != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(sprite);
            }
        }

        /// <summary>Creates a single stat row with a label name and colored value for the info panel.</summary>
        private static VisualElement BuildStatLine(string label, string value, Color valueColor)
        {
            VisualElement row = new();
            row.AddToClassList("lf-stat-row");

            Label nameLabel = new(label);
            nameLabel.AddToClassList("lf-stat-name");

            Label valueLabel = new(value);
            valueLabel.AddToClassList("lf-stat-value");
            valueLabel.style.color = valueColor;

            row.Add(nameLabel);
            row.Add(valueLabel);
            return row;
        }

        /// <summary>Updates the info panel for assembly mode with tool stats, preview sprite, and traits.</summary>
        private void UpdateInfoPanelAssembly(ToolInstance tool)
        {
            if (_statsContainer == null)
            {
                return;
            }

            _statsContainer.Clear();

            if (_traitsContainer != null)
            {
                _traitsContainer.Clear();
            }

            if (tool == null)
            {
                if (_resultLabel != null)
                {
                    _resultLabel.text = "Place parts to assemble";
                }

                if (_previewImage != null)
                {
                    _previewImage.style.backgroundImage = StyleKeyword.None;
                }

                return;
            }

            // Tool name
            string toolName = _selectedToolType.ToString();
            ResourceId headMat = RepairKitHelper.GetHeadMaterial(tool);

            if (!string.IsNullOrEmpty(headMat.Name))
            {
                string matName = headMat.Name;
                matName = char.ToUpper(matName[0]) + matName.Substring(1);
                toolName = matName + " " + toolName;
            }

            if (_resultLabel != null)
            {
                _resultLabel.text = toolName;
            }

            // Preview sprite
            if (_previewImage != null && Context.ToolPartTextures != null)
            {
                Sprite composite = ToolSpriteCompositor.Composite(
                    tool, Context.ToolPartTextures);

                if (composite != null)
                {
                    _previewImage.style.backgroundImage = new StyleBackground(composite);
                }
                else
                {
                    _previewImage.style.backgroundImage = StyleKeyword.None;
                }
            }

            // Stat lines
            _statsContainer.Add(BuildStatLine(
                "Durability", tool.MaxDurability.ToString(), s_statWhite));
            _statsContainer.Add(BuildStatLine(
                "Mining Speed", tool.BaseSpeed.ToString("F1"), s_statWhite));

            // Mining level name
            string levelName = tool.EffectiveToolLevel switch
            {
                0 => "Wood",
                1 => "Stone",
                2 => "Iron",
                3 => "Diamond",
                4 => "Netherite",
                _ => "Lvl " + tool.EffectiveToolLevel,
            };
            _statsContainer.Add(BuildStatLine("Mining Level", levelName, s_statYellow));
            _statsContainer.Add(BuildStatLine(
                "Attack Damage", tool.BaseDamage.ToString("F1"), s_statWhite));

            // Traits
            if (_traitsContainer != null && _traitRegistry != null)
            {
                IToolTrait[] traits = tool.GetAllTraits(_traitRegistry);

                for (int i = 0; i < traits.Length; i++)
                {
                    string traitName = FormatTraitName(traits[i].TraitId);
                    Label traitLabel = new(traitName);
                    traitLabel.AddToClassList("lf-trait-label");
                    _traitsContainer.Add(traitLabel);
                }
            }
        }

        /// <summary>Updates the info panel for repair mode with durability before/after and repair amount.</summary>
        private void UpdateInfoPanelRepair(ToolInstance tool, int currentDur, int maxDur, int repairAmount)
        {
            if (_statsContainer == null)
            {
                return;
            }

            _statsContainer.Clear();

            if (_traitsContainer != null)
            {
                _traitsContainer.Clear();
            }

            if (tool == null)
            {
                if (_resultLabel != null)
                {
                    _resultLabel.text = "Add matching repair material";
                }

                return;
            }

            // Tool name
            string toolName = tool.ToolType.ToString();
            ResourceId headMat = RepairKitHelper.GetHeadMaterial(tool);

            if (!string.IsNullOrEmpty(headMat.Name))
            {
                string matName = headMat.Name;
                matName = char.ToUpper(matName[0]) + matName.Substring(1);
                toolName = matName + " " + toolName;
            }

            if (_resultLabel != null)
            {
                _resultLabel.text = toolName;
            }

            // Preview sprite
            if (_previewImage != null)
            {
                ResourceId toolItemId = GetResultItemId(tool);
                Sprite existing = SpriteAtlas.Get(toolItemId);

                if (existing != null)
                {
                    _previewImage.style.backgroundImage = new StyleBackground(existing);
                }
            }

            // Current durability line
            _statsContainer.Add(BuildStatLine(
                "Durability", currentDur + " / " + maxDur, s_statWhite));

            if (repairAmount > 0)
            {
                int afterDur = Math.Min(currentDur + repairAmount, maxDur);
                _statsContainer.Add(BuildStatLine(
                    "After Repair", afterDur + " / " + maxDur, s_statGreen));
                _statsContainer.Add(BuildStatLine(
                    "Restores", "+" + repairAmount, s_statGreen));
            }
            else
            {
                _statsContainer.Add(BuildStatLine(
                    "", "Add matching material", s_statRed));
            }
        }

        /// <summary>Formats a trait ID (e.g. "lithforge:magnetic") into a display name (e.g. "Magnetic").</summary>
        private static string FormatTraitName(string traitId)
        {
            if (string.IsNullOrEmpty(traitId))
            {
                return string.Empty;
            }

            int colonIdx = traitId.IndexOf(':');

            if (colonIdx >= 0 && colonIdx < traitId.Length - 1)
            {
                traitId = traitId.Substring(colonIdx + 1);
            }

            if (traitId.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpper(traitId[0]) + traitId.Substring(1);
        }

        /// <summary>Applies the selected CSS class to the active tool type button and removes it from others.</summary>
        private void UpdateToolTypeButtonStyles()
        {
            SetButtonSelected(_pickaxeBtn, _selectedToolType == ToolType.Pickaxe);
            SetButtonSelected(_axeBtn, _selectedToolType == ToolType.Axe);
            SetButtonSelected(_shovelBtn, _selectedToolType == ToolType.Shovel);
            SetButtonSelected(_swordBtn, _selectedToolType == ToolType.Sword);
        }

        /// <summary>Toggles the selected CSS class on a button based on the given state.</summary>
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

        /// <summary>Handles pointer-down on slots: output take for assembly/repair, shift-click transfers, and regular clicks.</summary>
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

        /// <summary>Assembles a tool from the current parts, generates a composite sprite, and gives the result to the player.</summary>
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

            ItemStack resultStack = new(resultItemId, 1)
            {
                Durability = tool.MaxDurability,
            };
            DataComponentMap toolMap = new();
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

        /// <summary>Returns true if the head slot contains an existing tool (ToolInstance component), indicating repair mode.</summary>
        private bool IsRepairMode()
        {
            ItemStack headSlot = _headAdapter.GetSlot(0);

            if (headSlot.IsEmpty || !headSlot.HasComponents)
            {
                return false;
            }

            return headSlot.Components.Has(DataComponentTypes.ToolInstanceId);
        }

        /// <summary>Computes the repair preview by checking handle/binding slots for matching materials and updating the output slot.</summary>
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

                if (kitComp is
                    {
                        PartData:
                        {
                            PartType: ToolPartType.RepairKit,
                        },
                    } &&
                    kitComp.PartData.MaterialId.Equals(headMaterial))
                {
                    ToolMaterialData matData = Context.ToolMaterialRegistry?.Get(headMaterial);

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

                int needed = (int)Math.Ceiling((float)remaining / repairPerItem);
                int used = Math.Min(needed, stack.Count);
                itemsConsumed[i] = used;
                totalRepair += used * repairPerItem;
            }

            totalRepair = Math.Min(totalRepair, damageToRepair);

            if (totalRepair <= 0)
            {
                _outputAdapter.SetSlot(0, ItemStack.Empty);
                _repairItemsConsumed = null;
                _repairTotalRepair = 0;
                return;
            }

            ToolInstance repaired = headToolComp.Tool.Clone();
            int baseCurrentDur = repaired.IsBroken ? 0 : repaired.CurrentDurability;
            repaired.SetCurrentDurability(baseCurrentDur + totalRepair);

            ItemStack previewStack = toolStack;
            previewStack.Durability = repaired.CurrentDurability;
            DataComponentMap repairedMap = new();
            repairedMap.Set(DataComponentTypes.ToolInstanceId,
                new ToolInstanceComponent(repaired));
            previewStack.Components = repairedMap;

            _outputAdapter.SetSlot(0, previewStack);

            _repairItemsConsumed = itemsConsumed;
            _repairTotalRepair = totalRepair;

            // Info panel updated by Update() → UpdateInfoPanelRepair()
        }

        /// <summary>Gives the repaired tool to the player, consumes repair materials, and clears the head slot.</summary>
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

        /// <summary>Builds the result item ResourceId from the tool type and head material using naming convention.</summary>
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

        /// <summary>Returns held items to the player inventory and clears the station reference on close.</summary>
        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);
            _currentStation = null;
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
