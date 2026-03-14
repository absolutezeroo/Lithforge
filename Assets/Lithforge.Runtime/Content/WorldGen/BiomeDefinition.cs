using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.WorldGen
{
    [CreateAssetMenu(fileName = "NewBiome", menuName = "Lithforge/Content/Biome Definition", order = 7)]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("_biomeName"),Tooltip("Biome name")]
        [SerializeField] private string biomeName = "";

        [FormerlySerializedAs("_temperatureMin"),Header("Climate Range")]
        [Tooltip("Minimum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMin;

        [FormerlySerializedAs("_temperatureMax"),Tooltip("Maximum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMax = 1.0f;

        [FormerlySerializedAs("_temperatureCenter"),Tooltip("Preferred temperature center")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureCenter = 0.5f;

        [FormerlySerializedAs("_humidityMin"),Tooltip("Minimum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMin;

        [FormerlySerializedAs("_humidityMax"),Tooltip("Maximum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMax = 1.0f;

        [FormerlySerializedAs("_humidityCenter"),Tooltip("Preferred humidity center")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityCenter = 0.5f;

        [FormerlySerializedAs("_topBlock"),Header("Surface Blocks")]
        [Tooltip("Top surface block (e.g. grass_block)")]
        [SerializeField] private BlockDefinition topBlock;

        [FormerlySerializedAs("_fillerBlock"),Tooltip("Filler block below surface (e.g. dirt)")]
        [SerializeField] private BlockDefinition fillerBlock;

        [FormerlySerializedAs("_stoneBlock"),Tooltip("Stone block")]
        [SerializeField] private BlockDefinition stoneBlock;

        [FormerlySerializedAs("_underwaterBlock"),Tooltip("Block used underwater")]
        [SerializeField] private BlockDefinition underwaterBlock;

        [FormerlySerializedAs("_fillerDepth"),Header("Terrain")]
        [Tooltip("Depth of filler blocks")]
        [Min(0)]
        [SerializeField] private int fillerDepth = 3;

        [FormerlySerializedAs("_treeDensity"),Tooltip("Tree density (0 = no trees, 1 = maximum)")]
        [Range(0f, 1f)]
        [SerializeField] private float treeDensity;

        [FormerlySerializedAs("_treeType"),Tooltip("Tree shape variant for this biome (0=oak, 1=birch, 2=spruce)")]
        [Range(0, 2)]
        [SerializeField] private int treeType;

        [FormerlySerializedAs("_continentalnessCenter"),Header("Extended Climate")]
        [Tooltip("Preferred continentalness value (0=ocean, 1=far inland)")]
        [Range(0f, 1f)]
        [SerializeField] private float continentalnessCenter = 0.5f;

        [FormerlySerializedAs("_erosionCenter"),Tooltip("Preferred erosion value (0=low erosion/peaks, 1=high erosion/flat)")]
        [Range(0f, 1f)]
        [SerializeField] private float erosionCenter = 0.5f;

        [FormerlySerializedAs("_baseHeight"),Tooltip("Target surface height relative to sea level (blended per-biome)")]
        [SerializeField] private float baseHeight = 4f;

        [FormerlySerializedAs("_heightAmplitude"),Tooltip("Terrain noise amplitude scale for this biome")]
        [SerializeField] private float heightAmplitude = 12f;

        [FormerlySerializedAs("_weightSharpness"),Tooltip("Sharpness of the exponential weight falloff (higher = harder edges)")]
        [Range(1f, 32f)]
        [SerializeField] private float weightSharpness = 8.0f;

        [FormerlySerializedAs("_isOcean"),Header("Surface Behavior")]
        [Tooltip("Ocean biome: suppresses trees, uses UnderwaterBlock for floor")]
        [SerializeField] private bool isOcean;

        [FormerlySerializedAs("_isFrozen"),Tooltip("Frozen biome: places ice at water surface")]
        [SerializeField] private bool isFrozen;

        [FormerlySerializedAs("_isBeach"),Tooltip("Beach biome: reserved for future shore-specific behavior")]
        [SerializeField] private bool isBeach;

        [FormerlySerializedAs("_waterColor"),Header("Tinting")]
        [Tooltip("Water tint color for this biome")]
        [SerializeField] private Color waterColor = new Color(0.247f, 0.463f, 0.894f, 1f);

        [FormerlySerializedAs("_mapColor"),Header("Map")]
        [Tooltip("Color shown on the world map")]
        [SerializeField] private Color mapColor = Color.green;

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BiomeName
        {
            get { return biomeName; }
        }

        public float TemperatureMin
        {
            get { return temperatureMin; }
        }

        public float TemperatureMax
        {
            get { return temperatureMax; }
        }

        public float TemperatureCenter
        {
            get { return temperatureCenter; }
        }

        public float HumidityMin
        {
            get { return humidityMin; }
        }

        public float HumidityMax
        {
            get { return humidityMax; }
        }

        public float HumidityCenter
        {
            get { return humidityCenter; }
        }

        public BlockDefinition TopBlock
        {
            get { return topBlock; }
        }

        public BlockDefinition FillerBlock
        {
            get { return fillerBlock; }
        }

        public BlockDefinition StoneBlock
        {
            get { return stoneBlock; }
        }

        public BlockDefinition UnderwaterBlock
        {
            get { return underwaterBlock; }
        }

        public int FillerDepth
        {
            get { return fillerDepth; }
        }

        public float TreeDensity
        {
            get { return treeDensity; }
        }

        public int TreeType
        {
            get { return treeType; }
        }

        public float ContinentalnessCenter
        {
            get { return continentalnessCenter; }
        }

        public float ErosionCenter
        {
            get { return erosionCenter; }
        }

        public float BaseHeight
        {
            get { return baseHeight; }
        }

        public float HeightAmplitude
        {
            get { return heightAmplitude; }
        }

        public float WeightSharpness
        {
            get { return weightSharpness; }
        }

        public bool IsOcean
        {
            get { return isOcean; }
        }

        public bool IsFrozen
        {
            get { return isFrozen; }
        }

        public bool IsBeach
        {
            get { return isBeach; }
        }

        public Color WaterColor
        {
            get { return waterColor; }
        }

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
