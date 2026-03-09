using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    public sealed class OreDefinition
    {
        public ResourceId Id { get; }
        public ResourceId OreBlock { get; set; }
        public ResourceId ReplaceBlock { get; set; }
        public int MinY { get; set; }
        public int MaxY { get; set; }
        public int VeinSize { get; set; }
        public float Frequency { get; set; }
        public string OreType { get; set; }

        public OreDefinition(ResourceId id)
        {
            Id = id;
            OreType = "blob";
        }
    }
}
