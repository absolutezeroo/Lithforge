using System.Collections.Generic;

using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Runtime.UI.Widgets;
using Lithforge.Item;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Abstract base for all container screens (inventory, chest, furnace, etc.).
    ///     Creates a UIDocument, loads USS themes, builds slot grids,
    ///     wires pointer events to SlotInteractionController, and runs the per-frame refresh loop.
    ///     Subclasses call <see cref="InitializeBase(ScreenContext, int, string)" /> and handle
    ///     screen-specific logic via <see cref="OnSlotPointerDown" /> and <see cref="OnClose" />.
    /// </summary>
    public abstract class ContainerScreen : MonoBehaviour, IContainerScreen
    {
        // Slot widgets organized by container name + index
        private readonly List<SlotWidgetBinding> _allBindings = new();
        private DragGhostWidget _dragGhost;

        private VisualTreeAsset _screenTemplate;
        private TooltipWidget _tooltip;
        private int _openGraceFrames;

        // Tooltip key-change refresh state
        private bool _lastTooltipShift;
        private bool _lastTooltipCtrl;
        private float _lastTooltipX;
        private float _lastTooltipY;

        protected UIDocument Document { get; private set; }

        protected VisualElement Root { get; private set; }

        protected VisualElement Panel { get; private set; }

        protected SlotInteractionController Interaction { get; private set; }

        protected ItemSpriteAtlas SpriteAtlas { get; private set; }

        protected ItemRegistry ItemRegistryRef { get; private set; }

        /// <summary>
        ///     The shared context carrying all screen dependencies.
        ///     Available after <see cref="InitializeBase" /> has been called.
        /// </summary>
        protected ScreenContext Context { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>
        ///     Decrements grace frame counter. Call at the start of subclass Update().
        ///     Returns true if inputs should be ignored this frame.
        /// </summary>
        protected bool IsInGracePeriod()
        {
            if (_openGraceFrames > 0)
            {
                _openGraceFrames--;
                return true;
            }

            return false;
        }

        public void Close()
        {
            IsOpen = false;
            Panel.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Interaction.ResetState();
            _tooltip.Hide();
            _dragGhost.Refresh(ItemStack.Empty, SpriteAtlas);

            if (Context != null && Context.ScreenManager != null)
            {
                Context.ScreenManager.NotifyScreenClosed();
            }

            OnClose();
        }

        /// <summary>
        ///     Shows or hides the entire screen system (root document visibility).
        ///     Used by HudVisibilityController for spawn loading.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (Document != null && Document.rootVisualElement != null)
            {
                Document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        ///     Initialize the screen from a shared context. Creates the UIDocument,
        ///     loads USS themes, builds the overlay panel, tooltip, and drag ghost.
        ///     Also creates the <see cref="SlotInteractionController" /> internally.
        ///     If <paramref name="templatePath" /> is non-null, loads the corresponding
        ///     <see cref="VisualTreeAsset" /> via <c>Resources.Load</c> for use by
        ///     <see cref="CloneTemplate" />. Pass null (default) for imperative mode.
        ///     Call from subclass <c>Initialize(ScreenContext)</c>.
        /// </summary>
        protected void InitializeBase(ScreenContext context, int sortingOrder, string templatePath = null)
        {
            Context = context;

            HeldStack held = new();
            Interaction = new SlotInteractionController(
                held, context.ItemRegistry, context.ToolTemplateRegistry, context.ToolMaterialRegistry);
            SpriteAtlas = context.ItemSpriteAtlas;
            ItemRegistryRef = context.ItemRegistry;

            Document = gameObject.AddComponent<UIDocument>();
            Document.panelSettings = context.PanelSettings;
            Document.sortingOrder = sortingOrder;

            Root = Document.rootVisualElement;

            // Load USS themes
            StyleSheet variables = Resources.Load<StyleSheet>("UI/Themes/LithforgeVariables");
            StyleSheet theme = Resources.Load<StyleSheet>("UI/Themes/LithforgeDefault");

            if (variables != null)
            {
                Root.styleSheets.Add(variables);
            }

            if (theme != null)
            {
                Root.styleSheets.Add(theme);
            }

            // Build overlay and panel
            Panel = new VisualElement();
            Panel.AddToClassList("lf-overlay");
            Root.Add(Panel);

            // Pointer up to end paint mode
            Panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);

            // Mouse move for drag ghost and tooltip
            Panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);

            // Tooltip
            _tooltip = new TooltipWidget();
            Root.Add(_tooltip);

            // Drag ghost (on top of everything)
            _dragGhost = new DragGhostWidget();
            Root.Add(_dragGhost);

            // Start hidden
            Panel.style.display = DisplayStyle.None;
            IsOpen = false;

            // Optional UXML template — null means imperative mode
            if (templatePath != null)
            {
                _screenTemplate = Resources.Load<VisualTreeAsset>(templatePath);

                if (_screenTemplate == null)
                {
                    UnityEngine.Debug.LogError("[ContainerScreen] VisualTreeAsset not found at: " + templatePath);
                }
            }
        }

        /// <summary>
        ///     Clears all tracked slot widget bindings. Must be called at the top of
        ///     <c>RebuildUI()</c> before building new slot groups, so stale bindings
        ///     from a previous open do not accumulate.
        /// </summary>
        protected void ClearSlotBindings()
        {
            _allBindings.Clear();
        }

        /// <summary>
        ///     Clears slot bindings and panel content, then clones the stored
        ///     <see cref="VisualTreeAsset" /> directly into <see cref="Panel" />.
        ///     Returns false if no template was loaded or if <see cref="Panel" />
        ///     is null — the caller must early-return on false.
        ///     Call at the top of <c>RebuildUI()</c> in UXML-backed subclasses.
        /// </summary>
        protected bool CloneTemplate()
        {
            if (Panel == null)
            {
                UnityEngine.Debug.LogError("[ContainerScreen] CloneTemplate called before InitializeBase.");
                return false;
            }

            if (_screenTemplate == null)
            {
                UnityEngine.Debug.LogError("[" + GetType().Name + "] CloneTemplate: no VisualTreeAsset loaded. "
                                           + "Pass a non-null templatePath to InitializeBase.");
                return false;
            }

            ClearSlotBindings();
            Panel.Clear();
            _screenTemplate.CloneTree(Panel);
            return true;
        }

        /// <summary>
        ///     Queries <see cref="Panel" /> for a <see cref="VisualElement" /> by name.
        ///     Logs an error and returns null if not found. Use after <see cref="CloneTemplate" />
        ///     to locate named slot containers defined in the UXML template.
        /// </summary>
        protected VisualElement QueryContainer(string name)
        {
            if (Panel == null)
            {
                UnityEngine.Debug.LogError("[ContainerScreen] QueryContainer called before InitializeBase.");
                return null;
            }

            VisualElement result = Panel.Q<VisualElement>(name);

            if (result == null)
            {
                UnityEngine.Debug.LogError("[" + GetType().Name + "] QueryContainer: element '"
                                           + name + "' not found in UXML template.");
            }

            return result;
        }

        /// <summary>
        ///     Builds a slot group from a SlotGroupDefinition and wires pointer events.
        ///     Returns the container VisualElement holding the slot rows.
        /// </summary>
        protected VisualElement BuildSlotGroup(
            SlotGroupDefinition groupDef,
            ISlotContainer container,
            VisualElement parent)
        {
            VisualElement groupContainer = new();

            for (int row = 0; row < groupDef.Rows; row++)
            {
                VisualElement rowElement = new();
                rowElement.AddToClassList("lf-slot-row");

                for (int col = 0; col < groupDef.Columns; col++)
                {
                    int slotIndex = groupDef.StartIndex + row * groupDef.Columns + col;

                    if (slotIndex >= container.SlotCount)
                    {
                        break;
                    }

                    SlotWidget widget = new();
                    int capturedIndex = slotIndex;
                    ISlotContainer capturedContainer = container;

                    // Pointer down (guarded by grace period)
                    widget.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (_openGraceFrames > 0) return;
                        OnSlotPointerDown(capturedContainer, capturedIndex, evt);
                    });

                    // Hover
                    widget.RegisterCallback<PointerEnterEvent>(evt =>
                    {
                        Interaction.OnSlotEnter(capturedContainer, capturedIndex);
                        widget.AddToClassList("lf-slot--hovered");
                    });

                    widget.RegisterCallback<PointerLeaveEvent>(evt =>
                    {
                        Interaction.OnSlotLeave(capturedContainer, capturedIndex);
                        widget.RemoveFromClassList("lf-slot--hovered");
                        _tooltip.Hide();
                    });

                    rowElement.Add(widget);
                    _allBindings.Add(new SlotWidgetBinding(container, slotIndex, widget));
                }

                groupContainer.Add(rowElement);
            }

            parent.Add(groupContainer);
            return groupContainer;
        }

        /// <summary>
        ///     Builds a single slot (e.g. output slot) and wires pointer events.
        /// </summary>
        protected SlotWidget BuildSingleSlot(ISlotContainer container, int slotIndex, VisualElement parent)
        {
            SlotWidget widget = new();
            int capturedIndex = slotIndex;
            ISlotContainer capturedContainer = container;

            widget.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_openGraceFrames > 0) return;
                OnSlotPointerDown(capturedContainer, capturedIndex, evt);
            });

            widget.RegisterCallback<PointerEnterEvent>(evt =>
            {
                Interaction.OnSlotEnter(capturedContainer, capturedIndex);
                widget.AddToClassList("lf-slot--hovered");
            });

            widget.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                Interaction.OnSlotLeave(capturedContainer, capturedIndex);
                widget.RemoveFromClassList("lf-slot--hovered");
                _tooltip.Hide();
            });

            parent.Add(widget);
            _allBindings.Add(new SlotWidgetBinding(container, slotIndex, widget));
            return widget;
        }

        /// <summary>
        ///     Override to handle slot pointer down with screen-specific logic
        ///     (e.g. output slot take, shift-click transfer).
        /// </summary>
        protected abstract void OnSlotPointerDown(ISlotContainer container, int slotIndex, PointerDownEvent evt);

        /// <summary>
        ///     Refreshes all slot widgets. Call from subclass Update().
        /// </summary>
        protected void RefreshAllSlots()
        {
            ToolPartTextureDatabase toolTexDb = Context != null
                ? Context.ToolPartTextures : null;

            for (int i = 0; i < _allBindings.Count; i++)
            {
                SlotWidgetBinding binding = _allBindings[i];
                ItemStack stack = binding.Container.GetSlot(binding.SlotIndex);
                binding.Widget.Refresh(stack, SpriteAtlas, ItemRegistryRef, toolTexDb);
            }

            // Update drag ghost
            _dragGhost.Refresh(Interaction.Held.Stack, SpriteAtlas);
        }

        /// <summary>
        ///     Polls modifier key state and refreshes the tooltip if Shift or Ctrl
        ///     changed since the last pointer move. Call from subclass Update()
        ///     after <see cref="RefreshAllSlots" />.
        /// </summary>
        protected void UpdateTooltipKeyRefresh()
        {
            if (!IsOpen || _tooltip == null || _tooltip.style.display != DisplayStyle.Flex)
            {
                return;
            }

            bool shift = IsShiftHeld();
            bool ctrl = IsCtrlHeld();

            if (shift == _lastTooltipShift && ctrl == _lastTooltipCtrl)
            {
                return;
            }

            _lastTooltipShift = shift;
            _lastTooltipCtrl = ctrl;

            ISlotContainer hoveredContainer = Interaction.HoveredContainer;
            int hoveredIndex = Interaction.HoveredSlotIndex;

            if (hoveredContainer != null && hoveredIndex >= 0)
            {
                ItemStack stack = hoveredContainer.GetSlot(hoveredIndex);

                if (!stack.IsEmpty && ItemRegistryRef != null)
                {
                    ItemEntry entry = ItemRegistryRef.Get(stack.ItemId);
                    ToolMaterialRegistry matReg = Context != null
                        ? Context.ToolMaterialRegistry : null;
                    _tooltip.Show(stack, entry, _lastTooltipX, _lastTooltipY,
                        shift, ctrl, matReg);
                }
            }
        }

        public void Open()
        {
            IsOpen = true;
            Panel.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _openGraceFrames = 2;
            Interaction.ResetState();

            // Invalidate all slots so they redraw and clear residual hover classes
            for (int i = 0; i < _allBindings.Count; i++)
            {
                _allBindings[i].Widget.Invalidate();
                _allBindings[i].Widget.RemoveFromClassList("lf-slot--hovered");
            }
        }

        /// <summary>
        ///     Override to handle screen-specific close logic (return items, clear crafting grid).
        /// </summary>
        protected abstract void OnClose();

        private void OnPanelPointerUp(PointerUpEvent evt)
        {
            Interaction.OnPointerUp(evt.button);
        }

        private void OnPanelPointerMove(PointerMoveEvent evt)
        {
            // Update drag ghost position
            _dragGhost.UpdatePosition(evt.position.x, evt.position.y);

            // Update tooltip position and content
            ISlotContainer hoveredContainer = Interaction.HoveredContainer;
            int hoveredIndex = Interaction.HoveredSlotIndex;

            if (hoveredContainer != null && hoveredIndex >= 0 && Interaction.Held.IsEmpty)
            {
                ItemStack stack = hoveredContainer.GetSlot(hoveredIndex);

                if (!stack.IsEmpty && ItemRegistryRef != null)
                {
                    bool isShift = IsShiftHeld();
                    bool isCtrl = IsCtrlHeld();

                    _lastTooltipX = evt.position.x;
                    _lastTooltipY = evt.position.y;
                    _lastTooltipShift = isShift;
                    _lastTooltipCtrl = isCtrl;

                    ItemEntry entry = ItemRegistryRef.Get(stack.ItemId);
                    ToolMaterialRegistry matReg = Context != null
                        ? Context.ToolMaterialRegistry : null;
                    _tooltip.Show(stack, entry, evt.position.x, evt.position.y,
                        isShift, isCtrl, matReg);
                }
                else
                {
                    _tooltip.Hide();
                }
            }
            else
            {
                _tooltip.Hide();
            }
        }

        private static bool IsShiftHeld()
        {
            return Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);
        }

        private static bool IsCtrlHeld()
        {
            return Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed ||
                 Keyboard.current.rightCtrlKey.isPressed);
        }

        /// <summary>
        ///     Binding between a slot container index and its visual widget.
        /// </summary>
        private struct SlotWidgetBinding
        {
            public readonly ISlotContainer Container;
            public readonly int SlotIndex;
            public readonly SlotWidget Widget;

            public SlotWidgetBinding(ISlotContainer container, int slotIndex, SlotWidget widget)
            {
                Container = container;
                SlotIndex = slotIndex;
                Widget = widget;
            }
        }
    }
}
