using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Represents a crafting grid (2x2 for player inventory or 3x3 for crafting table).
    /// Stores ResourceId per slot (default = empty).
    /// </summary>
    public sealed class CraftingGrid
    {
        private readonly ResourceId[] _slots;
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
            _slots = new ResourceId[width * height];
        }

        public ResourceId GetSlot(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return default;
            }

            return _slots[y * _width + x];
        }

        public void SetSlot(int x, int y, ResourceId itemId)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _slots[y * _width + x] = itemId;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = default;
            }
        }

        /// <summary>
        /// Returns true if all slots are empty (default ResourceId).
        /// </summary>
        public bool IsEmpty()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Namespace != null)
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
                    ResourceId id = _slots[y * _width + x];

                    if (id.Namespace != null)
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
