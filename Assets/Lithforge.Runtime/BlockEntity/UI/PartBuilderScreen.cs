using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.BlockEntity.UI
{
    /// <summary>
    ///     Screen for the Part Builder block entity (TiC-faithful).
    ///     Shows pattern buttons on the left, Pattern + Material input slots,
    ///     an output slot, and info labels. Player inventory and hotbar below.
    /// </summary>
    public sealed class PartBuilderScreen : ContainerScreen
    {
        private readonly List<PartBuilderRecipe> _availableRecipes = new();
        private readonly List<Button> _patternButtons = new();
        private Label _costLabel;
        private PartBuilderBlockEntity _currentBuilder;
        private Label _haveLabel;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;
        private BlockEntityContainerAdapter _materialAdapter;
        private Label _materialLabel;

        private bool _needsRefresh = true;
        private BlockEntityContainerAdapter _outputAdapter;

        private BlockEntityContainerAdapter _patternAdapter;

        // UI elements
        private VisualElement _patternButtonContainer;
        private ToolMaterialData _resolvedMaterial;

        // Pattern selection state
        private int _selectedPatternIndex = -1;
        private PartBuilderRecipe _selectedRecipe;

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

            if (_needsRefresh)
            {
                _needsRefresh = false;
                RefreshPatternButtons();
            }

            RefreshAllSlots();
        }

        public void Initialize(ScreenContext context)
        {
            _hotbarAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, 0, Inventory.HotbarSize);
            _mainAdapter = new InventoryContainerAdapter(
                context.PlayerInventory, Inventory.HotbarSize,
                Inventory.SlotCount - Inventory.HotbarSize);

            InitializeBase(context, 265);

            Panel.style.display = DisplayStyle.None;
        }

        public void OpenForEntity(BlockEntity entity)
        {
            PartBuilderBlockEntity builder = entity as PartBuilderBlockEntity;

            if (builder == null)
            {
                return;
            }

            _currentBuilder = builder;

            InventoryBehavior inv = builder.Inventory;

            _patternAdapter = new BlockEntityContainerAdapter(
                inv, PartBuilderBlockEntity.PatternSlot, 1);
            _materialAdapter = new BlockEntityContainerAdapter(
                inv, PartBuilderBlockEntity.MaterialSlot, 1);
            _outputAdapter = new BlockEntityContainerAdapter(
                inv, PartBuilderBlockEntity.OutputSlot, 1, true);

            _selectedPatternIndex = -1;
            _selectedRecipe = null;
            _resolvedMaterial = null;
            _needsRefresh = true;

            RebuildUI();
            Open();
        }

        private void RebuildUI()
        {
            ClearSlotBindings();
            Panel.Clear();
            Panel.AddToClassList("lf-overlay");

            VisualElement container = new();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Title
            Label title = new("Part Builder");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Main content row: pattern buttons | slots
            VisualElement contentRow = new();
            contentRow.style.flexDirection = FlexDirection.Row;
            contentRow.style.marginBottom = 8;
            container.Add(contentRow);

            // Pattern buttons column (left side)
            VisualElement patternColumn = new();
            patternColumn.style.width = 160;
            patternColumn.style.marginRight = 8;
            contentRow.Add(patternColumn);

            Label patternLabel = new("Patterns");
            patternLabel.AddToClassList("lf-section-label");
            patternColumn.Add(patternLabel);

            // Scrollable pattern button container (4 columns)
            ScrollView scrollView = new(ScrollViewMode.Vertical);
            scrollView.style.maxHeight = 120;
            patternColumn.Add(scrollView);

            _patternButtonContainer = new VisualElement();
            _patternButtonContainer.style.flexDirection = FlexDirection.Row;
            _patternButtonContainer.style.flexWrap = Wrap.Wrap;
            scrollView.Add(_patternButtonContainer);

            // Slots column (right side)
            VisualElement slotsColumn = new();
            contentRow.Add(slotsColumn);

            // Input slots row
            VisualElement inputRow = new();
            inputRow.AddToClassList("lf-craft-section");
            slotsColumn.Add(inputRow);

            // Pattern slot
            VisualElement patternSlotCol = new();
            Label patternSlotLabel = new("Pattern");
            patternSlotLabel.AddToClassList("lf-section-label");
            patternSlotCol.Add(patternSlotLabel);
            BuildSingleSlot(_patternAdapter, 0, patternSlotCol);
            inputRow.Add(patternSlotCol);

            // Material slot
            VisualElement materialSlotCol = new();
            Label materialSlotLabel = new("Material");
            materialSlotLabel.AddToClassList("lf-section-label");
            materialSlotCol.Add(materialSlotLabel);
            BuildSingleSlot(_materialAdapter, 0, materialSlotCol);
            inputRow.Add(materialSlotCol);

            // Arrow
            VisualElement arrowCol = new();
            arrowCol.style.justifyContent = Justify.Center;
            arrowCol.style.alignItems = Align.Center;
            Label arrow = new("=>");
            arrow.AddToClassList("lf-craft-arrow");
            arrowCol.Add(arrow);
            inputRow.Add(arrowCol);

            // Output slot
            VisualElement outputCol = new();
            Label outputLabel = new("Result");
            outputLabel.AddToClassList("lf-section-label");
            outputCol.Add(outputLabel);
            BuildSingleSlot(_outputAdapter, 0, outputCol);
            inputRow.Add(outputCol);

            // Info labels
            _materialLabel = new Label("");
            _materialLabel.AddToClassList("lf-section-label");
            _materialLabel.style.marginTop = 4;
            slotsColumn.Add(_materialLabel);

            _costLabel = new Label("");
            _costLabel.AddToClassList("lf-section-label");
            _costLabel.style.fontSize = 10;
            slotsColumn.Add(_costLabel);

            _haveLabel = new Label("");
            _haveLabel.AddToClassList("lf-section-label");
            _haveLabel.style.fontSize = 10;
            slotsColumn.Add(_haveLabel);

            // Separator
            VisualElement sep1 = new();
            sep1.AddToClassList("lf-separator");
            container.Add(sep1);

            // Main inventory: 9x3
            Label mainLabel = new("Inventory");
            mainLabel.AddToClassList("lf-section-label");
            container.Add(mainLabel);

            SlotGroupDefinition mainGroupDef = SlotGroupDefinition.Create("main", 9, 3);
            BuildSlotGroup(mainGroupDef, _mainAdapter, container);

            // Separator
            VisualElement sep2 = new();
            sep2.AddToClassList("lf-separator");
            sep2.style.marginTop = 8;
            container.Add(sep2);

            // Hotbar: 9x1
            Label hotbarLabel = new("Hotbar");
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

            // Any slot interaction may change state
            _needsRefresh = true;

            // Output slot: take crafted part
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
                if (container == _patternAdapter || container == _materialAdapter)
                {
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _mainAdapter, _hotbarAdapter, ItemRegistryRef);
                }
                else if (container == _mainAdapter || container == _hotbarAdapter)
                {
                    ContainerTransfer.TransferItem(
                        container, slotIndex, _patternAdapter, _materialAdapter, ItemRegistryRef);
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
            if (_currentBuilder == null || _selectedRecipe == null || _resolvedMaterial == null)
            {
                return;
            }

            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);
            ItemStack patternStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.PatternSlot);

            if (materialStack.IsEmpty || patternStack.IsEmpty)
            {
                return;
            }

            int cost = GetEffectiveCost(_selectedRecipe);

            if (materialStack.Count < cost)
            {
                return;
            }

            // Build result
            ToolPartData partData = new()
            {
                PartType = _selectedRecipe.ResultPartType, MaterialId = _resolvedMaterial.MaterialId,
            };
            byte[] customData = ToolPartDataSerializer.Serialize(partData);

            ResourceId resultId = _selectedRecipe.ResultItemId;
            ItemStack resultStack = new(resultId, _selectedRecipe.ResultCount);
            resultStack.CustomData = customData;

            // Generate sprite if not cached
            string matSuffix = Context.ToolPartTextures != null
                ? Context.ToolPartTextures.ResolveSuffix(_resolvedMaterial.MaterialId)
                : _resolvedMaterial.MaterialId.Name;

            ResourceId spriteCacheKey = new(
                resultId.Namespace,
                resultId.Name + "__" + _resolvedMaterial.MaterialId.Name);

            if (!SpriteAtlas.Contains(spriteCacheKey))
            {
                if (Context.ToolPartTextures != null)
                {
                    Texture2D tex = Context.ToolPartTextures.FindPartTexture(
                        _selectedRecipe.ResultPartType, matSuffix);

                    if (tex != null)
                    {
                        Sprite sprite = Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f),
                            tex.width);
                        SpriteAtlas.Register(spriteCacheKey, sprite);
                    }
                }
            }

            // Give to player
            bool isShift = Keyboard.current != null &&
                           (Keyboard.current.leftShiftKey.isPressed ||
                            Keyboard.current.rightShiftKey.isPressed);

            bool given = false;

            if (isShift)
            {
                int leftOver = Context.PlayerInventory.AddItemStack(resultStack);
                given = leftOver == 0;
            }
            else if (Interaction.Held.IsEmpty)
            {
                Interaction.Held.Set(resultStack);
                given = true;
            }

            if (!given)
            {
                return;
            }

            // Consume material
            ItemStack updatedMaterial = materialStack;
            updatedMaterial.Count -= cost;

            if (updatedMaterial.Count <= 0)
            {
                updatedMaterial = ItemStack.Empty;
            }

            _currentBuilder.Inventory.SetSlot(
                PartBuilderBlockEntity.MaterialSlot, updatedMaterial);

            // Consume pattern (unless tagged "pattern_reusable")
            ItemEntry patternDef = ItemRegistryRef.Get(patternStack.ItemId);
            bool isReusable = patternDef != null && patternDef.Tags != null &&
                              patternDef.Tags.Contains("pattern_reusable");

            if (!isReusable)
            {
                ItemStack updatedPattern = patternStack;
                updatedPattern.Count -= 1;

                if (updatedPattern.Count <= 0)
                {
                    updatedPattern = ItemStack.Empty;
                }

                _currentBuilder.Inventory.SetSlot(
                    PartBuilderBlockEntity.PatternSlot, updatedPattern);
            }

            _needsRefresh = true;
        }

        private void RefreshPatternButtons()
        {
            _availableRecipes.Clear();
            _selectedRecipe = null;
            _selectedPatternIndex = -1;
            _resolvedMaterial = null;

            if (_currentBuilder == null)
            {
                RebuildPatternButtonsUI();
                UpdateInfoLabels();
                return;
            }

            // 1. Check pattern slot: must have item tagged "pattern"
            ItemStack patternStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.PatternSlot);

            if (patternStack.IsEmpty)
            {
                RebuildPatternButtonsUI();
                UpdateInfoLabels();
                return;
            }

            ItemEntry patternDef = ItemRegistryRef.Get(patternStack.ItemId);
            bool hasPatternTag = patternDef != null && patternDef.Tags != null &&
                                 patternDef.Tags.Contains("pattern");

            if (!hasPatternTag)
            {
                RebuildPatternButtonsUI();
                UpdateInfoLabels();
                return;
            }

            // 2. Resolve material from material slot
            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);

            if (!materialStack.IsEmpty)
            {
                _resolvedMaterial = Context.ToolMaterialRegistry.FindCraftableMaterialForItem(
                    materialStack.ItemId);
            }

            // 3. Filter recipes whose requiredPatternTag matches
            PartBuilderRecipeRegistry registry = Context.PartBuilderRecipeRegistry;

            for (int i = 0; i < registry.Recipes.Count; i++)
            {
                PartBuilderRecipe recipe = registry.Recipes[i];
                string reqTag = recipe.RequiredPatternTag;

                if (!string.IsNullOrEmpty(reqTag) && patternDef.Tags.Contains(reqTag))
                {
                    _availableRecipes.Add(recipe);
                }
            }

            // 4. Sort by cost then display name (TiC behavior)
            _availableRecipes.Sort((a, b) =>
            {
                int ca = GetEffectiveCost(a);
                int cb = GetEffectiveCost(b);
                int cmp = ca.CompareTo(cb);
                return cmp != 0 ? cmp : string.Compare(a.DisplayName, b.DisplayName);
            });

            RebuildPatternButtonsUI();
            UpdateInfoLabels();
        }

        private void RebuildPatternButtonsUI()
        {
            _patternButtonContainer.Clear();
            _patternButtons.Clear();

            for (int i = 0; i < _availableRecipes.Count; i++)
            {
                PartBuilderRecipe recipe = _availableRecipes[i];
                int capturedIndex = i;

                Button btn = new();
                btn.text = recipe.DisplayName;
                btn.style.width = 72;
                btn.style.height = 24;
                btn.style.marginRight = 2;
                btn.style.marginBottom = 2;

                // Disable if no material resolved or insufficient count
                bool canCraft = _resolvedMaterial != null;

                if (canCraft)
                {
                    ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                        PartBuilderBlockEntity.MaterialSlot);
                    int cost = GetEffectiveCost(recipe);
                    canCraft = materialStack.Count >= cost;
                }

                if (!canCraft)
                {
                    btn.style.opacity = 0.4f;
                }

                btn.clicked += () =>
                {
                    _selectedPatternIndex = capturedIndex;
                    _selectedRecipe = _availableRecipes[capturedIndex];
                    UpdatePatternButtonStyles();
                    UpdateInfoLabels();
                };

                _patternButtonContainer.Add(btn);
                _patternButtons.Add(btn);
            }

            UpdatePatternButtonStyles();
        }

        private void UpdatePatternButtonStyles()
        {
            for (int i = 0; i < _patternButtons.Count; i++)
            {
                Button btn = _patternButtons[i];
                bool selected = i == _selectedPatternIndex;

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
        }

        private void UpdateInfoLabels()
        {
            if (_selectedRecipe == null || _resolvedMaterial == null)
            {
                _materialLabel.text = _availableRecipes.Count > 0
                    ? "Select a pattern"
                    : "";
                _costLabel.text = "";
                _haveLabel.text = "";
                return;
            }

            // Material name (title case)
            string matName = _resolvedMaterial.MaterialId.Name;

            if (matName.Length > 0)
            {
                matName = char.ToUpper(matName[0]) + matName.Substring(1);
            }

            _materialLabel.text = matName + " " + _selectedRecipe.DisplayName;

            int cost = GetEffectiveCost(_selectedRecipe);
            _costLabel.text = "Cost: " + cost;

            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);
            _haveLabel.text = "Have: " + materialStack.Count;
        }

        private int GetEffectiveCost(PartBuilderRecipe recipe)
        {
            if (recipe.Cost > 0)
            {
                return recipe.Cost;
            }

            if (_resolvedMaterial != null)
            {
                return _resolvedMaterial.PartBuilderCost;
            }

            return 1;
        }

        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);

            // Return items from pattern and material slots to player inventory
            if (_currentBuilder != null)
            {
                for (int i = 0; i < PartBuilderBlockEntity.TotalSlotCount; i++)
                {
                    ItemStack stack = _currentBuilder.Inventory.GetSlot(i);

                    if (!stack.IsEmpty)
                    {
                        if (stack.HasCustomData)
                        {
                            Context.PlayerInventory.AddItemStack(stack);
                        }
                        else
                        {
                            Context.PlayerInventory.AddItem(stack.ItemId, stack.Count,
                                ItemRegistryRef.Get(stack.ItemId)?.MaxStackSize ?? 64);
                        }

                        _currentBuilder.Inventory.SetSlot(i, ItemStack.Empty);
                    }
                }
            }

            _currentBuilder = null;
        }
    }
}
