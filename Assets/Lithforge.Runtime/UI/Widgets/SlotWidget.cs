using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Item;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Widgets
{
    /// <summary>
    ///     Visual element representing a single inventory slot.
    ///     Displays an item icon sprite, stack count, and optional durability bar.
    ///     Uses USS class "lf-slot" for styling.
    ///     Marked as [UxmlElement] for future UXML template usage in UI Builder.
    /// </summary>
    [UxmlElement]
    public sealed partial class SlotWidget : VisualElement
    {
        /// <summary>Label displaying the item stack count.</summary>
        private readonly Label _count;

        /// <summary>The filled portion of the durability bar.</summary>
        private readonly VisualElement _durabilityFill;

        /// <summary>The background track of the durability bar.</summary>
        private readonly VisualElement _durabilityTrack;

        /// <summary>Image element displaying the item icon sprite.</summary>
        private readonly Image _icon;

        /// <summary>Whether this slot currently has the selected highlight.</summary>
        private bool _isSelected;

        /// <summary>Cached last item stack for dirty-check optimization.</summary>
        private ItemStack _lastStack;

        /// <summary>Creates a new SlotWidget with icon, count label, and durability bar elements.</summary>
        public SlotWidget()
        {
            AddToClassList("lf-slot");

            _icon = new Image();
            _icon.AddToClassList("lf-slot__icon");
            _icon.pickingMode = PickingMode.Ignore;
            Add(_icon);

            _count = new Label("");
            _count.AddToClassList("lf-slot__count");
            _count.pickingMode = PickingMode.Ignore;
            Add(_count);

            _durabilityTrack = new VisualElement();
            _durabilityTrack.AddToClassList("lf-slot__durability-track");
            _durabilityTrack.pickingMode = PickingMode.Ignore;
            _durabilityTrack.style.display = DisplayStyle.None;

            _durabilityFill = new VisualElement();
            _durabilityFill.AddToClassList("lf-slot__durability-fill");
            _durabilityFill.pickingMode = PickingMode.Ignore;
            _durabilityTrack.Add(_durabilityFill);
            Add(_durabilityTrack);
        }

        /// <summary>
        ///     The slot index this widget represents within its container.
        ///     Exposed as a UXML attribute for declarative layout in future phases.
        /// </summary>
        [UxmlAttribute]
        public int SlotIndex { get; set; } = -1;

        /// <summary>
        ///     Whether this slot is currently selected (e.g. hotbar highlight).
        ///     Setting this property toggles the "lf-slot--selected" USS class.
        /// </summary>
        [UxmlAttribute]
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                SetSelected(value);
            }
        }

        /// <summary>
        ///     Updates the slot visual to reflect the given item stack.
        ///     Only updates if the stack has changed (dirty check).
        /// </summary>
        public void Refresh(
            ItemStack stack,
            ItemSpriteAtlas atlas,
            ItemRegistry itemRegistry,
            ToolPartTextureDatabase toolPartTexDb = null)
        {
            if (stack.Equals(_lastStack))
            {
                return;
            }

            _lastStack = stack;

            if (stack.IsEmpty)
            {
                _icon.sprite = null;
                _icon.style.display = DisplayStyle.None;
                _count.text = "";
                _durabilityTrack.style.display = DisplayStyle.None;
                return;
            }

            // Icon
            if (atlas != null)
            {
                Sprite sprite = atlas.Get(stack.ItemId);

                // Generic tool parts: resolve sprite from component material
                ToolPartDataComponent partComp = stack.Components?.Get<ToolPartDataComponent>(
                    DataComponentTypes.ToolPartDataId);

                if (partComp != null)
                {
                    ToolPartData partData = partComp.PartData;
                    ResourceId cacheKey = new(
                        stack.ItemId.Namespace,
                        stack.ItemId.Name + "__" + partData.MaterialId.Name);

                    if (atlas.Contains(cacheKey))
                    {
                        sprite = atlas.Get(cacheKey);
                    }
                }

                // Re-composite assembled tools if sprite is missing (e.g. after save/load)
                if (!atlas.Contains(stack.ItemId) &&
                    stack.HasComponents && toolPartTexDb != null)
                {
                    ToolInstanceComponent toolComp = stack.Components.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);

                    if (toolComp != null)
                    {
                        Sprite composite = ToolSpriteCompositor.Composite(
                            toolComp.Tool, toolPartTexDb);

                        if (composite != null)
                        {
                            atlas.Register(stack.ItemId, composite);
                            sprite = composite;
                        }
                    }
                }

                _icon.sprite = sprite;
                _icon.style.display = DisplayStyle.Flex;
            }
            else
            {
                _icon.style.display = DisplayStyle.None;
            }

            // Count
            _count.text = stack.Count > 1 ? stack.Count.ToString() : "";

            // Durability bar (from modular ToolInstance)
            ToolInstanceComponent durToolComp = stack.Components?.Get<ToolInstanceComponent>(
                DataComponentTypes.ToolInstanceId);

            if (durToolComp != null)
            {
                ToolInstance tool = durToolComp.Tool;

                if (tool.MaxDurability > 0)
                {
                    float ratio = (float)tool.CurrentDurability / tool.MaxDurability;
                    _durabilityTrack.style.display = DisplayStyle.Flex;
                    _durabilityFill.style.width = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));

                    // Color by ratio
                    Color barColor;

                    if (ratio > 0.5f)
                    {
                        barColor = new Color(0f, 0.78f, 0f, 1f);
                    }
                    else if (ratio > 0.25f)
                    {
                        barColor = new Color(0.78f, 0.78f, 0f, 1f);
                    }
                    else
                    {
                        barColor = new Color(0.78f, 0f, 0f, 1f);
                    }

                    _durabilityFill.style.backgroundColor = barColor;
                }
                else
                {
                    _durabilityTrack.style.display = DisplayStyle.None;
                }
            }
            else
            {
                _durabilityTrack.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        ///     Forces a full refresh by clearing the cached state.
        /// </summary>
        public void Invalidate()
        {
            _lastStack = default;
            _icon.sprite = null;
            _icon.style.display = DisplayStyle.None;
            _count.text = "";
            _durabilityTrack.style.display = DisplayStyle.None;
        }

        /// <summary>
        ///     Adds or removes the selected border highlight.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selected)
            {
                AddToClassList("lf-slot--selected");
            }
            else
            {
                RemoveFromClassList("lf-slot--selected");
            }
        }
    }
}
