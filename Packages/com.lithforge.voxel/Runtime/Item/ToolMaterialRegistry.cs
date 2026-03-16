using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Registry mapping material ResourceIds to resolved ToolMaterialData.
    /// Built during content pipeline from ToolMaterialDefinition assets.
    /// </summary>
    public sealed class ToolMaterialRegistry
    {
        private readonly Dictionary<ResourceId, ToolMaterialData> _materials;

        public ToolMaterialRegistry()
        {
            _materials = new Dictionary<ResourceId, ToolMaterialData>();
        }

        public void Register(ToolMaterialData material)
        {
            _materials[material.MaterialId] = material;
        }

        public ToolMaterialData Get(ResourceId materialId)
        {
            if (_materials.TryGetValue(materialId, out ToolMaterialData data))
            {
                return data;
            }

            return null;
        }

        public bool Contains(ResourceId materialId)
        {
            return _materials.ContainsKey(materialId);
        }

        public int Count
        {
            get { return _materials.Count; }
        }

        /// <summary>
        /// Finds the craftable material that accepts the given item as input in the Part Builder.
        /// Returns null if no match or material is not craftable.
        /// </summary>
        public ToolMaterialData FindCraftableMaterialForItem(ResourceId itemId)
        {
            string itemIdStr = itemId.ToString();

            foreach (KeyValuePair<ResourceId, ToolMaterialData> kvp in _materials)
            {
                ToolMaterialData mat = kvp.Value;

                if (!mat.IsCraftable)
                {
                    continue;
                }

                if (mat.CraftingItemIds == null)
                {
                    continue;
                }

                for (int i = 0; i < mat.CraftingItemIds.Length; i++)
                {
                    if (mat.CraftingItemIds[i] == itemIdStr)
                    {
                        return mat;
                    }
                }
            }

            return null;
        }
    }
}
