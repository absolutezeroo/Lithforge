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
        /// <summary>Normalized temperature value (0-1) sampled from climate noise.</summary>
        public float Temperature;

        /// <summary>Normalized humidity value (0-1) sampled from climate noise.</summary>
        public float Humidity;

        /// <summary>Normalized continentalness value (0-1). Low = ocean, high = inland.</summary>
        public float Continentalness;

        /// <summary>Normalized erosion value (0-1). Low = mountainous, high = flat.</summary>
        public float Erosion;
    }
}