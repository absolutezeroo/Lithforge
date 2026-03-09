using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    public sealed class BiomeDefinition
    {
        public ResourceId Id { get; }
        public float TemperatureMin { get; set; }
        public float TemperatureMax { get; set; }
        public float TemperatureCenter { get; set; }
        public float HumidityMin { get; set; }
        public float HumidityMax { get; set; }
        public float HumidityCenter { get; set; }
        public ResourceId TopBlock { get; set; }
        public ResourceId FillerBlock { get; set; }
        public ResourceId StoneBlock { get; set; }
        public ResourceId UnderwaterBlock { get; set; }
        public int FillerDepth { get; set; }
        public float TreeDensity { get; set; }
        public float HeightModifier { get; set; }

        public BiomeDefinition(ResourceId id)
        {
            Id = id;
            FillerDepth = 3;
        }
    }
}
