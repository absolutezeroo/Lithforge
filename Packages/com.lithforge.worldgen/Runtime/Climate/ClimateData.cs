using System.Runtime.InteropServices;

namespace Lithforge.WorldGen.Climate
{
    /// <summary>
    /// Per-column climate values sampled by ClimateNoiseJob.
    /// All values are normalized to [0, 1] from raw noise output.
    /// Consumed by TerrainShapeJob for biome-weighted height blending
    /// and dominant biome selection.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ClimateData
    {
        public float Temperature;
        public float Humidity;
        public float Continentalness;
        public float Erosion;
    }
}