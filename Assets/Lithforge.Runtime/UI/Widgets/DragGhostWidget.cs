using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Item;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Widgets
{
    /// <summary>
    /// Absolute-positioned icon that follows the cursor while an item is held.
    /// PickingMode.Ignore so it never intercepts pointer events.
    /// Uses USS class "lf-drag-ghost".
    /// </summary>
    public sealed class DragGhostWidget : VisualElement
    {
        private readonly Image _icon;
        private readonly Label _count;

        public DragGhostWidget()
        {
            AddToClassList("lf-drag-ghost");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            _icon = new Image();
            _icon.AddToClassList("lf-drag-ghost__icon");
            _icon.pickingMode = PickingMode.Ignore;
            Add(_icon);

            _count = new Label("");
            _count.AddToClassList("lf-drag-ghost__count");
            _count.pickingMode = PickingMode.Ignore;
            Add(_count);
        }

        /// <summary>
        /// Updates the ghost to match the held item stack.
        /// Call every frame while inventory is open.
        /// </summary>
        public void Refresh(ItemStack stack, ItemSpriteAtlas atlas)
        {
            if (stack.IsEmpty)
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;

            if (atlas != null)
            {
                _icon.sprite = atlas.Get(stack.ItemId);
            }

            _count.text = stack.Count > 1 ? stack.Count.ToString() : "";
        }

        /// <summary>
        /// Moves the ghost to follow the cursor position.
        /// Position is in panel coordinates.
        /// </summary>
        public void UpdatePosition(float panelX, float panelY)
        {
            // Offset so the icon is centered on the cursor
            style.left = panelX - 30;
            style.top = panelY - 30;
        }
    }
}
