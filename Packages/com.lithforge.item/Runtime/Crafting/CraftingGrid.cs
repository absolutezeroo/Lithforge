using Lithforge.Core.Data;
using Lithforge.Item;

namespace Lithforge.Item.Crafting
{
    /// <summary>
    /// Represents a crafting grid (2x2 for player inventory or 3x3 for crafting table).
    /// Stores ItemStack per slot (default = empty).
    /// </summary>
    public sealed class CraftingGrid : IItemStorage
    {
        private readonly ItemStack[] _slots;

        public int Width { get; }

        public int Height { get; }

        public CraftingGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _slots = new ItemStack[width * height];
        }

        /// <summary>
        /// Returns the ItemStack at the given grid position.
        /// </summary>
        public ItemStack GetSlotStack(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return ItemStack.Empty;
            }

            return _slots[y * Width + x];
        }

        /// <summary>
        /// Sets the ItemStack at the given grid position.
        /// </summary>
        public void SetSlotStack(int x, int y, ItemStack stack)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                _slots[y * Width + x] = stack;
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

        /// <summary>Total number of slots in the grid (Width * Height).</summary>
        public int SlotCount
        {
            get { return _slots.Length; }
        }

        /// <summary>Returns the ItemStack at the given linear index (row-major: index = y*Width+x).</summary>
        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
            {
                return ItemStack.Empty;
            }

            return _slots[index];
        }

        /// <summary>Sets the ItemStack at the given linear index (row-major: index = y*Width+x).</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            if (index >= 0 && index < _slots.Length)
            {
                _slots[index] = stack;
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
            int x0 = Width;
            int y0 = Height;
            int x1 = -1;
            int y1 = -1;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    ItemStack stack = _slots[y * Width + x];

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
