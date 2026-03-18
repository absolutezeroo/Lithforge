using Lithforge.Voxel.Item;

namespace Lithforge.Item
{
    /// <summary>
    /// Wraps a <see cref="ToolInstance"/> as a data component.
    /// Uses reference equality (tools are unique instances, never stacked).
    /// </summary>
    public sealed class ToolInstanceComponent : IDataComponent
    {
        public ToolInstance Tool { get; }

        public ToolInstanceComponent(ToolInstance tool)
        {
            Tool = tool;
        }
    }
}
