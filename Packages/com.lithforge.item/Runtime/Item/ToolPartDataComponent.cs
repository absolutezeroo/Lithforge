namespace Lithforge.Item
{
    /// <summary>
    /// Wraps a <see cref="ToolPartData"/> as a data component.
    /// Value equality on PartType + MaterialId.
    /// </summary>
    public sealed class ToolPartDataComponent : IDataComponent
    {
        public ToolPartData PartData { get; }

        public ToolPartDataComponent(ToolPartData partData)
        {
            PartData = partData;
        }

        public override bool Equals(object obj)
        {
            ToolPartDataComponent other = obj as ToolPartDataComponent;

            if (other == null)
            {
                return false;
            }

            return PartData.PartType == other.PartData.PartType
                && PartData.MaterialId == other.PartData.MaterialId;
        }

        public override int GetHashCode()
        {
            return ((int)PartData.PartType * 397) ^ PartData.MaterialId.GetHashCode();
        }
    }
}
