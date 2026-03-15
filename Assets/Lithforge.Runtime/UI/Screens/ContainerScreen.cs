using System.Collections.Generic;
using Lithforge.Runtime.UI.Container;
using Lithforge.Runtime.UI.Interaction;
using Lithforge.Runtime.UI.Layout;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Runtime.UI.Widgets;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Abstract base for all container screens (inventory, chest, furnace, etc.).
    /// Creates a UIDocument, loads USS themes, builds slot grids,
    /// wires pointer events to SlotInteractionController, and runs the per-frame refresh loop.
    /// Subclasses call <see cref="InitializeBase(ScreenContext, int)"/> and handle
    /// screen-specific logic via <see cref="OnSlotPointerDown"/> and <see cref="OnClose"/>.
    /// </summary>
    public abstract class ContainerScreen : MonoBehaviour, IContainerScreen
    {
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _panel;
        private TooltipWidget _tooltip;
        private DragGhostWidget _dragGhost;

        private SlotInteractionController _interaction;
        private ItemSpriteAtlas _spriteAtlas;
        private ItemRegistry _itemRegistry;
        private ScreenContext _context;

        private bool _isOpen;

        // Slot widgets organized by container name + index
        private readonly List<SlotWidgetBinding> _allBindings = new List<SlotWidgetBinding>();

        protected UIDocument Document
        {
            get { return _document; }
        }

        protected VisualElement Root
        {
            get { return _root; }
        }

        protected VisualElement Panel
        {
            get { return _panel; }
        }

        public bool IsOpen
        {
            get { return _isOpen; }
        }

        protected SlotInteractionController Interaction
        {
            get { return _interaction; }
        }

        protected ItemSpriteAtlas SpriteAtlas
        {
            get { return _spriteAtlas; }
        }

        protected ItemRegistry ItemRegistryRef
        {
            get { return _itemRegistry; }
        }

        /// <summary>
        /// The shared context carrying all screen dependencies.
        /// Available after <see cref="InitializeBase"/> has been called.
        /// </summary>
        protected ScreenContext Context
        {
            get { return _context; }
        }

        /// <summary>
        /// Initialize the screen from a shared context. Creates the UIDocument,
        /// loads USS themes, builds the overlay panel, tooltip, and drag ghost.
        /// Also creates the <see cref="SlotInteractionController"/> internally.
        /// Call from subclass <c>Initialize(ScreenContext)</c>.
        /// </summary>
        protected void InitializeBase(ScreenContext context, int sortingOrder)
        {
            _context = context;

            HeldStack held = new HeldStack();
            _interaction = new SlotInteractionController(held, context.ItemRegistry);
            _spriteAtlas = context.ItemSpriteAtlas;
            _itemRegistry = context.ItemRegistry;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = context.PanelSettings;
            _document.sortingOrder = sortingOrder;

            _root = _document.rootVisualElement;

            // Load USS themes
            StyleSheet variables = Resources.Load<StyleSheet>("UI/Themes/LithforgeVariables");
            StyleSheet theme = Resources.Load<StyleSheet>("UI/Themes/LithforgeDefault");

            if (variables != null)
            {
                _root.styleSheets.Add(variables);
            }

            if (theme != null)
            {
                _root.styleSheets.Add(theme);
            }

            // Build overlay and panel
            _panel = new VisualElement();
            _panel.AddToClassList("lf-overlay");
            _root.Add(_panel);

            // Pointer up to end paint mode
            _panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);

            // Mouse move for drag ghost and tooltip
            _panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);

            // Tooltip
            _tooltip = new TooltipWidget();
            _root.Add(_tooltip);

            // Drag ghost (on top of everything)
            _dragGhost = new DragGhostWidget();
            _root.Add(_dragGhost);

            // Start hidden
            _panel.style.display = DisplayStyle.None;
            _isOpen = false;
        }

        /// <summary>
        /// Clears all tracked slot widget bindings. Must be called at the top of
        /// <c>RebuildUI()</c> before building new slot groups, so stale bindings
        /// from a previous open do not accumulate.
        /// </summary>
        protected void ClearSlotBindings()
        {
            _allBindings.Clear();
        }

        /// <summary>
        /// Builds a slot group from a SlotGroupDefinition and wires pointer events.
        /// Returns the container VisualElement holding the slot rows.
        /// </summary>
        protected VisualElement BuildSlotGroup(
            SlotGroupDefinition groupDef,
            ISlotContainer container,
            VisualElement parent)
        {
            VisualElement groupContainer = new VisualElement();

            for (int row = 0; row < groupDef.Rows; row++)
            {
                VisualElement rowElement = new VisualElement();
                rowElement.AddToClassList("lf-slot-row");

                for (int col = 0; col < groupDef.Columns; col++)
                {
                    int slotIndex = groupDef.StartIndex + row * groupDef.Columns + col;

                    if (slotIndex >= container.SlotCount)
                    {
                        break;
                    }

                    SlotWidget widget = new SlotWidget();
                    int capturedIndex = slotIndex;
                    ISlotContainer capturedContainer = container;

                    // Pointer down
                    widget.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        OnSlotPointerDown(capturedContainer, capturedIndex, evt);
                    });

                    // Hover
                    widget.RegisterCallback<PointerEnterEvent>(evt =>
                    {
                        _interaction.OnSlotEnter(capturedContainer, capturedIndex);
                    });

                    widget.RegisterCallback<PointerLeaveEvent>(evt =>
                    {
                        _interaction.OnSlotLeave(capturedContainer, capturedIndex);
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
        /// Builds a single slot (e.g. output slot) and wires pointer events.
        /// </summary>
        protected SlotWidget BuildSingleSlot(ISlotContainer container, int slotIndex, VisualElement parent)
        {
            SlotWidget widget = new SlotWidget();
            int capturedIndex = slotIndex;
            ISlotContainer capturedContainer = container;

            widget.RegisterCallback<PointerDownEvent>(evt =>
            {
                OnSlotPointerDown(capturedContainer, capturedIndex, evt);
            });

            widget.RegisterCallback<PointerEnterEvent>(evt =>
            {
                _interaction.OnSlotEnter(capturedContainer, capturedIndex);
            });

            widget.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                _interaction.OnSlotLeave(capturedContainer, capturedIndex);
                _tooltip.Hide();
            });

            parent.Add(widget);
            _allBindings.Add(new SlotWidgetBinding(container, slotIndex, widget));
            return widget;
        }

        /// <summary>
        /// Override to handle slot pointer down with screen-specific logic
        /// (e.g. output slot take, shift-click transfer).
        /// </summary>
        protected abstract void OnSlotPointerDown(ISlotContainer container, int slotIndex, PointerDownEvent evt);

        /// <summary>
        /// Refreshes all slot widgets. Call from subclass Update().
        /// </summary>
        protected void RefreshAllSlots()
        {
            for (int i = 0; i < _allBindings.Count; i++)
            {
                SlotWidgetBinding binding = _allBindings[i];
                ItemStack stack = binding.Container.GetSlot(binding.SlotIndex);
                binding.Widget.Refresh(stack, _spriteAtlas, _itemRegistry);
            }

            // Update drag ghost
            _dragGhost.Refresh(_interaction.Held.Stack, _spriteAtlas);
        }

        public void Open()
        {
            _isOpen = true;
            _panel.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Invalidate all slots so they redraw
            for (int i = 0; i < _allBindings.Count; i++)
            {
                _allBindings[i].Widget.Invalidate();
            }
        }

        public void Close()
        {
            _isOpen = false;
            _panel.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _interaction.ResetState();
            _tooltip.Hide();
            _dragGhost.Refresh(ItemStack.Empty, _spriteAtlas);

            OnClose();
        }

        /// <summary>
        /// Override to handle screen-specific close logic (return items, clear crafting grid).
        /// </summary>
        protected abstract void OnClose();

        /// <summary>
        /// Shows or hides the entire screen system (root document visibility).
        /// Used by HudVisibilityController for spawn loading.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnPanelPointerUp(PointerUpEvent evt)
        {
            _interaction.OnPointerUp(evt.button);
        }

        private void OnPanelPointerMove(PointerMoveEvent evt)
        {
            // Update drag ghost position
            _dragGhost.UpdatePosition(evt.position.x, evt.position.y);

            // Update tooltip position and content
            ISlotContainer hoveredContainer = _interaction.HoveredContainer;
            int hoveredIndex = _interaction.HoveredSlotIndex;

            if (hoveredContainer != null && hoveredIndex >= 0 && _interaction.Held.IsEmpty)
            {
                ItemStack stack = hoveredContainer.GetSlot(hoveredIndex);

                if (!stack.IsEmpty && _itemRegistry != null)
                {
                    ItemEntry entry = _itemRegistry.Get(stack.ItemId);
                    _tooltip.Show(stack, entry, evt.position.x, evt.position.y);
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

        /// <summary>
        /// Binding between a slot container index and its visual widget.
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
