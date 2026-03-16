using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Data stored in ItemStack.CustomData for generic tool part items.
    /// Encodes the part type and material.
    /// </summary>
    public struct ToolPartData
    {
        public ToolPartType PartType;
        public ResourceId MaterialId;
    }
}
