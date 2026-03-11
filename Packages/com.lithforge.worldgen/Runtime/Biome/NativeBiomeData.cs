using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;

namespace Lithforge.WorldGen.Biome
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeBiomeData
    {
        public byte BiomeId;
        public float TemperatureMin;
        public float TemperatureMax;
        public float TemperatureCenter;
        public float HumidityMin;
        public float HumidityMax;
        public float HumidityCenter;
        public StateId TopBlock;
        public StateId FillerBlock;
        public StateId StoneBlock;
        public StateId UnderwaterBlock;
        public byte FillerDepth;
        public float TreeDensity;
        public float HeightModifier;
        public byte TreeTemplateIndex;
    }
}
