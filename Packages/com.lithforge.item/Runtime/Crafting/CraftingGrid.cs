using Lithforge.Core.Data;
using Lithforge.Item;

namespace Lithforge.Item.Crafting
{
    /// <summary>
    /// Represents a crafting grid (2x2 for player inventory or 3x3 for crafting table).
    /// Stores ItemStack per slot (default = empty).
    /// </summary>
    public sealed class CraftingGrid
    {
        private readonly ItemStack[] _slots;
        private readonly int _width;
        private readonly int _height;

        public int Width
        {
            get { return _width; }
        }

        public int Height
        {
            get { return _height; }
        }

        public CraftingGrid(int width, int height)
        {
            _width = width;
            _height = height;
            _slots = new ItemStack[width * height];
        }

        /// <summary>
        /// Returns the ItemStack at the given grid position.
        /// </summary>
        public ItemStack GetSlotStack(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return ItemStack.Empty;
            }

            return _slots[y * _width + x];
        }

        /// <summary>
        /// Sets the ItemStack at the given grid position.
        /// </summary>
        public void SetSlotStack(int x, int y, ItemStack stack)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _slots[y * _width + x] = stack;
            }
        }

        /// <summary>
        /// Returns the ResourceId at the given grid position (backward compat for CraftingEngine).
        /// </summary>
        public ResourceId GetSlot(int x, int y)
        {
            ItemStack stack = GetSlotStack(x, y);
            return stack.IsEmpty ? default : stack.ItemId;
        }

        /// <summary>
        /// Sets a slot by ResourceId with count=1 (backward compat).
        /// </summary>
        public void SetSlot(int x, int y, ResourceId itemId)
        {
            if (itemId.Namespace != null)
            {
                SetSlotStack(x, y, new ItemStack(itemId, 1));
            }
            else
            {
                SetSlotStack(x, y, ItemStack.Empty);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = ItemStack.Empty;
            }
        }

        /// <summary>
        /// Returns true if all slots are empty.
        /// </summary>
        public bool IsEmpty()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Computes the bounding rectangle of non-empty slots.
        /// Returns (minX, minY, width, height). Returns (0,0,0,0) if empty.
        /// </summary>
        public void GetBounds(out int minX, out int minY, out int boundsWidth, out int boundsHeight)
        {
            int x0 = _width;
            int y0 = _height;
            int x1 = -1;
            int y1 = -1;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    ItemStack stack = _slots[y * _width + x];

                    if (!stack.IsEmpty)
                    {
                        if (x < x0) { x0 = x; }
                        if (y < y0) { y0 = y; }
                        if (x > x1) { x1 = x; }
                        if (y > y1) { y1 = y; }
                    }
                }
            }

            if (x1 < 0)
            {
                minX = 0;
                minY = 0;
                boundsWidth = 0;
                boundsHeight = 0;
            }
            else
            {
                minX = x0;
                minY = y0;
                boundsWidth = x1 - x0 + 1;
                boundsHeight = y1 - y0 + 1;
            }
        }
    }
}
