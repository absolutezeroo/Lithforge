using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Registry mapping item IDs to their MaterialInputData.
    /// An item can only map to one material (first registered wins).
    /// Equivalent to TiC's MaterialRecipeCache.
    /// </summary>
    public sealed class MaterialInputRegistry
    {
        private readonly Dictionary<ResourceId, MaterialInputData> _byItem = new();

        public void Register(MaterialInputData entry)
        {
            // First registered wins (like TiC)
            if (!_byItem.ContainsKey(entry.ItemId))
            {
                _byItem[entry.ItemId] = entry;
            }
        }

        /// <summary>
        /// Finds the material input data for the given item.
        /// Returns null if no match.
        /// </summary>
        public MaterialInputData Get(ResourceId itemId)
        {
            _byItem.TryGetValue(itemId, out MaterialInputData data);
            return data;
        }

        public int Count
        {
            get { return _byItem.Count; }
        }
    }
}
