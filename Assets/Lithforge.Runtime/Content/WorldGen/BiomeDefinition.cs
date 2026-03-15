using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.WorldGen
{
    /// <summary>
    /// Authored definition of a biome, specifying climate affinity, surface block palette,
    /// terrain shape, tree density, and visual tinting. Baked to <c>NativeBiomeData</c> at
    /// startup so Burst generation jobs can select biomes per column via weighted climate distance.
    /// </summary>
    /// <remarks>
    /// Biome selection uses a multi-axis distance function (temperature, humidity, continentalness,
    /// erosion) with exponential weight falloff controlled by <see cref="WeightSharpness"/>.
    /// The invariant <c>BiomeData[i].BiomeId == i</c> is asserted at startup to guarantee O(1)
    /// lookup by index in Burst jobs.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewBiome", menuName = "Lithforge/Content/Biome Definition", order = 7)]
    public sealed class BiomeDefinition : ScriptableObject
    {
        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Unique name within the namespace, forming the ResourceId "namespace:biomeName".</summary>
        [FormerlySerializedAs("_biomeName"),Tooltip("Biome name")]
        [SerializeField] private string biomeName = "";

        /// <summary>Lower bound of the temperature range where this biome can appear.</summary>
        [FormerlySerializedAs("_temperatureMin"),Header("Climate Range")]
        [Tooltip("Minimum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMin;

        /// <summary>Upper bound of the temperature range where this biome can appear.</summary>
        [FormerlySerializedAs("_temperatureMax"),Tooltip("Maximum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMax = 1.0f;

        /// <summary>Ideal temperature for this biome; distance from this value reduces selection weight.</summary>
        [FormerlySerializedAs("_temperatureCenter"),Tooltip("Preferred temperature center")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureCenter = 0.5f;

        /// <summary>Lower bound of the humidity range where this biome can appear.</summary>
        [FormerlySerializedAs("_humidityMin"),Tooltip("Minimum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMin;

        /// <summary>Upper bound of the humidity range where this biome can appear.</summary>
        [FormerlySerializedAs("_humidityMax"),Tooltip("Maximum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMax = 1.0f;

        /// <summary>Ideal humidity for this biome; distance from this value reduces selection weight.</summary>
        [FormerlySerializedAs("_humidityCenter"),Tooltip("Preferred humidity center")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityCenter = 0.5f;

        /// <summary>Topmost surface block placed by SurfaceBuilderJob (e.g. grass_block for plains).</summary>
        [FormerlySerializedAs("_topBlock"),Header("Surface Blocks")]
        [Tooltip("Top surface block (e.g. grass_block)")]
        [SerializeField] private BlockDefinition topBlock;

        /// <summary>Block placed in the layers between the top block and stone (e.g. dirt).</summary>
        [FormerlySerializedAs("_fillerBlock"),Tooltip("Filler block below surface (e.g. dirt)")]
        [SerializeField] private BlockDefinition fillerBlock;

        /// <summary>Bulk underground block below the filler layer.</summary>
        [FormerlySerializedAs("_stoneBlock"),Tooltip("Stone block")]
        [SerializeField] private BlockDefinition stoneBlock;

        /// <summary>Block used for the ocean or river floor when this biome is submerged.</summary>
        [FormerlySerializedAs("_underwaterBlock"),Tooltip("Block used underwater")]
        [SerializeField] private BlockDefinition underwaterBlock;

        /// <summary>How many layers of filler block are placed between the top block and stone.</summary>
        [FormerlySerializedAs("_fillerDepth"),Header("Terrain")]
        [Tooltip("Depth of filler blocks")]
        [Min(0)]
        [SerializeField] private int fillerDepth = 3;

        /// <summary>Probability weight for tree placement per surface column (0 = none, 1 = maximum).</summary>
        [FormerlySerializedAs("_treeDensity"),Tooltip("Tree density (0 = no trees, 1 = maximum)")]
        [Range(0f, 1f)]
        [SerializeField] private float treeDensity;

        /// <summary>Index into <c>TreeTemplate</c> variants: 0 = oak, 1 = birch, 2 = spruce.</summary>
        [FormerlySerializedAs("_treeType"),Tooltip("Tree shape variant for this biome (0=oak, 1=birch, 2=spruce)")]
        [Range(0, 2)]
        [SerializeField] private int treeType;

        /// <summary>Preferred position on the ocean-to-inland axis (0 = deep ocean, 1 = far inland).</summary>
        [FormerlySerializedAs("_continentalnessCenter"),Header("Extended Climate")]
        [Tooltip("Preferred continentalness value (0=ocean, 1=far inland)")]
        [Range(0f, 1f)]
        [SerializeField] private float continentalnessCenter = 0.5f;

        /// <summary>Preferred erosion level (0 = mountainous peaks, 1 = flat eroded plains).</summary>
        [FormerlySerializedAs("_erosionCenter"),Tooltip("Preferred erosion value (0=low erosion/peaks, 1=high erosion/flat)")]
        [Range(0f, 1f)]
        [SerializeField] private float erosionCenter = 0.5f;

        /// <summary>Target surface height offset relative to sea level, blended between neighboring biomes.</summary>
        [FormerlySerializedAs("_baseHeight"),Tooltip("Target surface height relative to sea level (blended per-biome)")]
        [SerializeField] private float baseHeight = 4f;

        /// <summary>Scales the amplitude of terrain noise for this biome (higher = more dramatic hills).</summary>
        [FormerlySerializedAs("_heightAmplitude"),Tooltip("Terrain noise amplitude scale for this biome")]
        [SerializeField] private float heightAmplitude = 12f;

        /// <summary>
        /// Controls how sharply this biome's influence drops off with climate distance.
        /// Higher values produce harder biome boundaries; lower values create smoother transitions.
        /// </summary>
        [FormerlySerializedAs("_weightSharpness"),Tooltip("Sharpness of the exponential weight falloff (higher = harder edges)")]
        [Range(1f, 32f)]
        [SerializeField] private float weightSharpness = 8.0f;

        /// <summary>When true, trees are suppressed and the underwater block is used for the floor.</summary>
        [FormerlySerializedAs("_isOcean"),Header("Surface Behavior")]
        [Tooltip("Ocean biome: suppresses trees, uses UnderwaterBlock for floor")]
        [SerializeField] private bool isOcean;

        /// <summary>When true, ice is placed at the water surface instead of open water.</summary>
        [FormerlySerializedAs("_isFrozen"),Tooltip("Frozen biome: places ice at water surface")]
        [SerializeField] private bool isFrozen;

        /// <summary>Marks this biome as a beach zone for shore-specific generation rules.</summary>
        [FormerlySerializedAs("_isBeach"),Tooltip("Beach biome: reserved for future shore-specific behavior")]
        [SerializeField] private bool isBeach;

        /// <summary>Tint applied to water faces rendered within this biome.</summary>
        [FormerlySerializedAs("_waterColor"),Header("Tinting")]
        [Tooltip("Water tint color for this biome")]
        [SerializeField] private Color waterColor = new Color(0.247f, 0.463f, 0.894f, 1f);

        /// <summary>Representative color used on the minimap and world overview.</summary>
        [FormerlySerializedAs("_mapColor"),Header("Map")]
        [Tooltip("Color shown on the world map")]
        [SerializeField] private Color mapColor = Color.green;

        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Unique name within the namespace, auto-populated from the asset name if left blank.</summary>
        public string BiomeName
        {
            get { return biomeName; }
        }

        /// <summary>Lower bound of the viable temperature range.</summary>
        public float TemperatureMin
        {
            get { return temperatureMin; }
        }

        /// <summary>Upper bound of the viable temperature range.</summary>
        public float TemperatureMax
        {
            get { return temperatureMax; }
        }

        /// <summary>Ideal temperature; used as the center point for distance-based weight calculation.</summary>
        public float TemperatureCenter
        {
            get { return temperatureCenter; }
        }

        /// <summary>Lower bound of the viable humidity range.</summary>
        public float HumidityMin
        {
            get { return humidityMin; }
        }

        /// <summary>Upper bound of the viable humidity range.</summary>
        public float HumidityMax
        {
            get { return humidityMax; }
        }

        /// <summary>Ideal humidity; used as the center point for distance-based weight calculation.</summary>
        public float HumidityCenter
        {
            get { return humidityCenter; }
        }

        /// <summary>Topmost surface block (e.g. grass_block, sand).</summary>
        public BlockDefinition TopBlock
        {
            get { return topBlock; }
        }

        /// <summary>Sub-surface block placed between the top and stone layers.</summary>
        public BlockDefinition FillerBlock
        {
            get { return fillerBlock; }
        }

        /// <summary>Bulk underground block below the filler layer.</summary>
        public BlockDefinition StoneBlock
        {
            get { return stoneBlock; }
        }

        /// <summary>Floor block used in submerged areas of this biome.</summary>
        public BlockDefinition UnderwaterBlock
        {
            get { return underwaterBlock; }
        }

        /// <summary>Number of filler-block layers between the top block and stone.</summary>
        public int FillerDepth
        {
            get { return fillerDepth; }
        }

        /// <summary>Probability weight for tree placement (0 = none, 1 = maximum).</summary>
        public float TreeDensity
        {
            get { return treeDensity; }
        }

        /// <summary>Tree template index: 0 = oak, 1 = birch, 2 = spruce.</summary>
        public int TreeType
        {
            get { return treeType; }
        }

        /// <summary>Preferred position on the ocean-to-inland continentalness axis.</summary>
        public float ContinentalnessCenter
        {
            get { return continentalnessCenter; }
        }

        /// <summary>Preferred erosion level (0 = peaks, 1 = flat).</summary>
        public float ErosionCenter
        {
            get { return erosionCenter; }
        }

        /// <summary>Target surface height offset relative to sea level.</summary>
        public float BaseHeight
        {
            get { return baseHeight; }
        }

        /// <summary>Terrain noise amplitude scale; higher values create more dramatic elevation variation.</summary>
        public float HeightAmplitude
        {
            get { return heightAmplitude; }
        }

        /// <summary>Exponential falloff sharpness for biome selection weights (1 = smooth, 32 = sharp).</summary>
        public float WeightSharpness
        {
            get { return weightSharpness; }
        }

        /// <summary>Whether this is an ocean biome (suppresses trees, uses underwater floor block).</summary>
        public bool IsOcean
        {
            get { return isOcean; }
        }

        /// <summary>Whether water surfaces freeze to ice in this biome.</summary>
        public bool IsFrozen
        {
            get { return isFrozen; }
        }

        /// <summary>Whether this biome occupies a beach/shore transition zone.</summary>
        public bool IsBeach
        {
            get { return isBeach; }
        }

        /// <summary>Biome-specific water tint color for the shader.</summary>
        public Color WaterColor
        {
            get { return waterColor; }
        }

        /// <summary>Representative color used on the minimap and world overview.</summary>
        public Color MapColor
        {
            get { return mapColor; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(biomeName))
            {
                biomeName = name;
            }
        }
    }
}
