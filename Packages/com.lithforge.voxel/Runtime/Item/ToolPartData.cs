using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    ///     Data for generic tool part items. Stored as ToolPartDataComponent.
    ///     Encodes the part type and material.
    /// </summary>
    public struct ToolPartData
    {
        public ToolPartType PartType;

        public ResourceId MaterialId;
    }
}
