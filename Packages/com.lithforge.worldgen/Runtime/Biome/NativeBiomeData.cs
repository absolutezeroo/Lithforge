using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;

namespace Lithforge.WorldGen.Biome
{
    /// <summary>
    /// Burst-compatible biome descriptor baked from a managed BiomeDefinition at content load.
    /// Passed as a NativeArray element to generation jobs (SurfaceBuilderJob, DecorationStage).
    /// <remarks>
    /// Invariant: the array index must equal <see cref="BiomeId"/> (O(1) lookup by direct index).
    /// This is asserted at startup in LithforgeBootstrap.
    /// </remarks>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeBiomeData
    {
        /// <summary>Biome index. Must match its position in the NativeArray.</summary>
        public byte BiomeId;

        /// <summary>Minimum temperature for biome selection noise matching.</summary>
        public float TemperatureMin;

        /// <summary>Maximum temperature for biome selection noise matching.</summary>
        public float TemperatureMax;

        /// <summary>Ideal temperature center point used for weighted distance scoring.</summary>
        public float TemperatureCenter;

        /// <summary>Minimum humidity for biome selection noise matching.</summary>
        public float HumidityMin;

        /// <summary>Maximum humidity for biome selection noise matching.</summary>
        public float HumidityMax;

        /// <summary>Ideal humidity center point used for weighted distance scoring.</summary>
        public float HumidityCenter;

        /// <summary>Surface layer block (e.g., grass, sand). Placed by SurfaceBuilderJob.</summary>
        public StateId TopBlock;

        /// <summary>Sub-surface filler block (e.g., dirt). Placed below TopBlock to FillerDepth.</summary>
        public StateId FillerBlock;

        /// <summary>Base stone block below the filler layer.</summary>
        public StateId StoneBlock;

        /// <summary>Block placed on ocean/river beds in this biome (e.g., clay, gravel).</summary>
        public StateId UnderwaterBlock;

        /// <summary>How many blocks deep the filler layer extends below the surface.</summary>
        public byte FillerDepth;

        /// <summary>Probability (0-1) of a tree spawning per eligible surface column.</summary>
        public float TreeDensity;

        /// <summary>Index into the tree template array (0=Oak, 1=Birch, 2=Spruce).</summary>
        public byte TreeTemplateIndex;

        /// <summary>Target continentalness value for biome placement (ocean vs. land gradient).</summary>
        public float ContinentalnessCenter;

        /// <summary>Target erosion value for biome placement (flat vs. mountainous).</summary>
        public float ErosionCenter;

        /// <summary>Sea-level-relative base terrain height for this biome in blocks.</summary>
        public float BaseHeight;

        /// <summary>Noise-driven height variation amplitude in blocks.</summary>
        public float HeightAmplitude;

        /// <summary>RGBA water tint packed as a uint, used by BiomeTintManager for water rendering.</summary>
        public uint WaterColorPacked;

        /// <summary>
        /// Controls how sharply this biome dominates in weighted selection.
        /// Higher values create harder biome boundaries (oceans use 20-25, land uses 7-8).
        /// </summary>
        public float WeightSharpness;

        /// <summary>Packed NativeBiomeSurfaceFlags (IsOcean, IsFrozen, IsBeach).</summary>
        public byte SurfaceFlags;
    }
}
