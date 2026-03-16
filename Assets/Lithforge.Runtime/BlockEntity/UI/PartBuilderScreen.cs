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
    ///     an output slot with live preview, and info labels. Player inventory and hotbar below.
    /// </summary>
    public sealed class PartBuilderScreen : ContainerScreen
    {
        private readonly List<PartBuilderRecipe> _availableRecipes = new List<PartBuilderRecipe>();
        private readonly List<Button> _patternButtons = new List<Button>();
        private Label _costLabel;
        private PartBuilderBlockEntity _currentBuilder;
        private Label _haveLabel;
        private InventoryContainerAdapter _hotbarAdapter;
        private InventoryContainerAdapter _mainAdapter;
        private BlockEntityContainerAdapter _materialAdapter;
        private Label _materialLabel;

        private bool _needsRefresh;
        private BlockEntityContainerAdapter _outputAdapter;

        private BlockEntityContainerAdapter _patternAdapter;

        // UI elements
        private VisualElement _patternButtonContainer;

        // Material resolution (TiC-style)
        private MaterialInputData _resolvedInput;
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
            _resolvedInput = null;
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

            VisualElement container = new VisualElement();
            container.AddToClassList("lf-panel");
            Panel.Add(container);

            // Title
            Label title = new Label("Part Builder");
            title.AddToClassList("lf-panel__title");
            container.Add(title);

            // Main content row: pattern buttons | slots
            VisualElement contentRow = new VisualElement();
            contentRow.style.flexDirection = FlexDirection.Row;
            contentRow.style.marginBottom = 8;
            container.Add(contentRow);

            // Pattern buttons column (left side)
            VisualElement patternColumn = new VisualElement();
            patternColumn.style.width = 160;
            patternColumn.style.marginRight = 8;
            contentRow.Add(patternColumn);

            Label patternLabel = new Label("Patterns");
            patternLabel.AddToClassList("lf-section-label");
            patternColumn.Add(patternLabel);

            // Scrollable pattern button container (4 columns)
            ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.maxHeight = 120;
            patternColumn.Add(scrollView);

            _patternButtonContainer = new VisualElement();
            _patternButtonContainer.style.flexDirection = FlexDirection.Row;
            _patternButtonContainer.style.flexWrap = Wrap.Wrap;
            scrollView.Add(_patternButtonContainer);

            // Slots column (right side)
            VisualElement slotsColumn = new VisualElement();
            contentRow.Add(slotsColumn);

            // Input slots row
            VisualElement inputRow = new VisualElement();
            inputRow.AddToClassList("lf-craft-section");
            slotsColumn.Add(inputRow);

            // Pattern slot
            VisualElement patternSlotCol = new VisualElement();
            Label patternSlotLabel = new Label("Pattern");
            patternSlotLabel.AddToClassList("lf-section-label");
            patternSlotCol.Add(patternSlotLabel);
            BuildSingleSlot(_patternAdapter, 0, patternSlotCol);
            inputRow.Add(patternSlotCol);

            // Material slot
            VisualElement materialSlotCol = new VisualElement();
            Label materialSlotLabel = new Label("Material");
            materialSlotLabel.AddToClassList("lf-section-label");
            materialSlotCol.Add(materialSlotLabel);
            BuildSingleSlot(_materialAdapter, 0, materialSlotCol);
            inputRow.Add(materialSlotCol);

            // Arrow
            VisualElement arrowCol = new VisualElement();
            arrowCol.style.justifyContent = Justify.Center;
            arrowCol.style.alignItems = Align.Center;
            Label arrow = new Label("=>");
            arrow.AddToClassList("lf-craft-arrow");
            arrowCol.Add(arrow);
            inputRow.Add(arrowCol);

            // Output slot
            VisualElement outputCol = new VisualElement();
            Label outputLabel = new Label("Result");
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
            if (_currentBuilder == null || _selectedRecipe == null ||
                _resolvedInput == null || _resolvedMaterial == null)
            {
                return;
            }

            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);
            ItemStack patternStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.PatternSlot);
            ItemStack outputStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.OutputSlot);

            if (materialStack.IsEmpty || patternStack.IsEmpty || outputStack.IsEmpty)
            {
                return;
            }

            int recipeCost = GetRecipeCost(_selectedRecipe);
            int itemsUsed = _resolvedInput.GetItemsUsed(recipeCost);

            if (materialStack.Count < itemsUsed)
            {
                return;
            }

            // Give result to player (output slot already contains the preview item)
            bool isShift = Keyboard.current != null &&
                           (Keyboard.current.leftShiftKey.isPressed ||
                            Keyboard.current.rightShiftKey.isPressed);

            bool given = false;

            if (isShift)
            {
                int leftOver = Context.PlayerInventory.AddItemStack(outputStack);
                given = leftOver == 0;
            }
            else if (Interaction.Held.IsEmpty)
            {
                Interaction.Held.Set(outputStack);
                given = true;
            }

            if (!given)
            {
                return;
            }

            // Clear output slot
            _currentBuilder.Inventory.SetSlot(PartBuilderBlockEntity.OutputSlot, ItemStack.Empty);

            // Consume material
            ItemStack updatedMaterial = materialStack;
            updatedMaterial.Count -= itemsUsed;

            if (updatedMaterial.Count <= 0)
            {
                updatedMaterial = ItemStack.Empty;
            }

            _currentBuilder.Inventory.SetSlot(
                PartBuilderBlockEntity.MaterialSlot, updatedMaterial);

            // Give leftover items (TiC: e.g. planks from log with excess value)
            int leftoverCount = _resolvedInput.GetLeftoverCount(recipeCost);

            if (leftoverCount > 0 && _resolvedInput.HasLeftover)
            {
                ItemStack leftoverStack = new ItemStack(
                    _resolvedInput.LeftoverItemId, leftoverCount);
                Context.PlayerInventory.AddItemStack(leftoverStack);
            }

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
            _resolvedInput = null;
            _resolvedMaterial = null;

            // Clear preview output
            if (_currentBuilder != null)
            {
                _currentBuilder.Inventory.SetSlot(
                    PartBuilderBlockEntity.OutputSlot, ItemStack.Empty);
            }

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

            // 2. Filter recipes whose requiredPatternTag matches (before material check,
            //    so buttons appear even with empty material slot — TiC partialMatch)
            PartBuilderRecipeRegistry registry = Context.PartBuilderRecipeRegistry;

            if (registry == null)
            {
                RebuildPatternButtonsUI();
                UpdateInfoLabels();
                return;
            }

            for (int i = 0; i < registry.Recipes.Count; i++)
            {
                PartBuilderRecipe recipe = registry.Recipes[i];
                string reqTag = recipe.RequiredPatternTag;

                if (!string.IsNullOrEmpty(reqTag) && patternDef.Tags.Contains(reqTag))
                {
                    _availableRecipes.Add(recipe);
                }
            }

            // 3. Resolve material from material slot (after recipes)
            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);

            if (!materialStack.IsEmpty)
            {
                _resolvedInput = Context.MaterialInputRegistry != null
                    ? Context.MaterialInputRegistry.Get(materialStack.ItemId)
                    : null;

                if (_resolvedInput != null && Context.ToolMaterialRegistry != null)
                {
                    _resolvedMaterial = Context.ToolMaterialRegistry.Get(
                        _resolvedInput.MaterialId);
                }
            }

            // 4. Sort by recipe cost then display name (TiC behavior)
            _availableRecipes.Sort((PartBuilderRecipe a, PartBuilderRecipe b) =>
            {
                int ca = GetRecipeCost(a);
                int cb = GetRecipeCost(b);
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

                Button btn = new Button();
                btn.text = recipe.DisplayName;
                btn.style.width = 72;
                btn.style.height = 24;
                btn.style.marginRight = 2;
                btn.style.marginBottom = 2;

                // Disable if no material resolved or insufficient count
                bool canCraft = _resolvedInput != null && _resolvedMaterial != null;

                if (canCraft)
                {
                    ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                        PartBuilderBlockEntity.MaterialSlot);
                    int itemsUsed = GetItemsUsed(recipe);
                    canCraft = materialStack.Count >= itemsUsed;
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
                    UpdatePreview();
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

        private void UpdatePreview()
        {
            if (_currentBuilder == null)
            {
                return;
            }

            if (_selectedRecipe == null || _resolvedInput == null || _resolvedMaterial == null)
            {
                _currentBuilder.Inventory.SetSlot(
                    PartBuilderBlockEntity.OutputSlot, ItemStack.Empty);
                return;
            }

            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);
            int itemsUsed = GetItemsUsed(_selectedRecipe);

            if (materialStack.IsEmpty || materialStack.Count < itemsUsed)
            {
                _currentBuilder.Inventory.SetSlot(
                    PartBuilderBlockEntity.OutputSlot, ItemStack.Empty);
                return;
            }

            // Build preview result
            ToolPartData partData = new ToolPartData
            {
                PartType = _selectedRecipe.ResultPartType,
                MaterialId = _resolvedMaterial.MaterialId,
            };
            byte[] customData = ToolPartDataSerializer.Serialize(partData);

            ResourceId resultId = _selectedRecipe.ResultItemId;
            int recipeCost = GetRecipeCost(_selectedRecipe);
            int resultCount = _selectedRecipe.ResultCount;

            // TiC bonus: if no leftover and per-item material value > cost, give extra parts
            // Per-item value = Value / Needed (e.g. stick: 1/2 = 0.5, log: 4/1 = 4)
            float perItemValue = _resolvedInput.Value / (float)_resolvedInput.Needed;

            if (!_resolvedInput.HasLeftover && perItemValue > recipeCost)
            {
                resultCount = (int)(_selectedRecipe.ResultCount * perItemValue / recipeCost);
                if (resultCount < 1)
                {
                    resultCount = 1;
                }
            }

            ItemStack previewResult = new ItemStack(resultId, resultCount);
            previewResult.CustomData = customData;

            // Ensure sprite is cached
            string matSuffix = Context.ToolPartTextures != null
                ? Context.ToolPartTextures.ResolveSuffix(_resolvedMaterial.MaterialId)
                : _resolvedMaterial.MaterialId.Name;

            ResourceId spriteCacheKey = new ResourceId(
                resultId.Namespace,
                resultId.Name + "__" + _resolvedMaterial.MaterialId.Name);

            if (!SpriteAtlas.Contains(spriteCacheKey) && Context.ToolPartTextures != null)
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

            // Set preview into output slot for display
            _currentBuilder.Inventory.SetSlot(
                PartBuilderBlockEntity.OutputSlot, previewResult);
        }

        private void UpdateInfoLabels()
        {
            if (_selectedRecipe == null)
            {
                _materialLabel.text = _availableRecipes.Count > 0
                    ? "Select a pattern"
                    : "";
                _costLabel.text = "";
                _haveLabel.text = "";
                return;
            }

            if (_resolvedMaterial == null || _resolvedInput == null)
            {
                // No material in slot — show recipe name but no cost info
                _materialLabel.text = _selectedRecipe.DisplayName;
                _costLabel.text = "Place material to craft";
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

            int recipeCost = GetRecipeCost(_selectedRecipe);
            int itemsUsed = _resolvedInput.GetItemsUsed(recipeCost);

            ItemStack materialStack = _currentBuilder.Inventory.GetSlot(
                PartBuilderBlockEntity.MaterialSlot);

            // Show material value as float (TiC shows "1.5 / 2.0")
            float haveValue = _resolvedInput.GetMaterialValue(materialStack.Count);

            _costLabel.text = "Cost: " + recipeCost + " (" + itemsUsed + " items)";
            _haveLabel.text = "Have: " + haveValue.ToString("F1") + " units (" +
                              materialStack.Count + " items)";
        }

        private int GetRecipeCost(PartBuilderRecipe recipe)
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

        private int GetItemsUsed(PartBuilderRecipe recipe)
        {
            if (_resolvedInput == null)
            {
                return 1;
            }

            int cost = GetRecipeCost(recipe);
            return _resolvedInput.GetItemsUsed(cost);
        }

        protected override void OnClose()
        {
            Interaction.ReturnHeldToInventory(Context.PlayerInventory);

            // Clear preview from output slot before returning items
            if (_currentBuilder != null)
            {
                _currentBuilder.Inventory.SetSlot(
                    PartBuilderBlockEntity.OutputSlot, ItemStack.Empty);
            }

            // Return items from pattern and material slots to player inventory
            // Skip output slot (index 2) — it contained a preview, not a real item
            if (_currentBuilder != null)
            {
                for (int i = 0; i < PartBuilderBlockEntity.TotalSlotCount; i++)
                {
                    if (i == PartBuilderBlockEntity.OutputSlot)
                    {
                        continue;
                    }

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
