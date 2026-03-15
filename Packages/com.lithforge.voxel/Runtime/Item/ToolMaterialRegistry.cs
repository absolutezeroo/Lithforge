using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Registry mapping material ResourceIds to resolved ToolMaterialData.
    /// Built during content pipeline from ToolMaterialDefinitionSO assets.
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
    }
}
