using Lithforge.Runtime.Content.Blocks;
using UnityEngine;

namespace Lithforge.Runtime.Content.WorldGen
{
    [CreateAssetMenu(fileName = "NewBiome", menuName = "Lithforge/Content/Biome Definition", order = 7)]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Biome name")]
        [SerializeField] private string biomeName = "";

        [Header("Climate Range")]
        [Tooltip("Minimum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMin;

        [Tooltip("Maximum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureMax = 1.0f;

        [Tooltip("Preferred temperature center")]
        [Range(0f, 1f)]
        [SerializeField] private float temperatureCenter = 0.5f;

        [Tooltip("Minimum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMin;

        [Tooltip("Maximum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityMax = 1.0f;

        [Tooltip("Preferred humidity center")]
        [Range(0f, 1f)]
        [SerializeField] private float humidityCenter = 0.5f;

        [Header("Surface Blocks")]
        [Tooltip("Top surface block (e.g. grass_block)")]
        [SerializeField] private BlockDefinition topBlock;

        [Tooltip("Filler block below surface (e.g. dirt)")]
        [SerializeField] private BlockDefinition fillerBlock;

        [Tooltip("Stone block")]
        [SerializeField] private BlockDefinition stoneBlock;

        [Tooltip("Block used underwater")]
        [SerializeField] private BlockDefinition underwaterBlock;

        [Header("Terrain")]
        [Tooltip("Depth of filler blocks")]
        [Min(0)]
        [SerializeField] private int fillerDepth = 3;

        [Tooltip("Tree density (0 = no trees, 1 = maximum)")]
        [Range(0f, 1f)]
        [SerializeField] private float treeDensity;

        [Tooltip("Height modifier for terrain generation")]
        [SerializeField] private float heightModifier;

        [Tooltip("Tree shape variant for this biome (0=oak, 1=birch, 2=spruce)")]
        [Range(0, 2)]
        [SerializeField] private int treeType;

        [Header("Extended Climate")]
        [Tooltip("Preferred continentalness value (0=ocean, 1=far inland)")]
        [Range(0f, 1f)]
        [SerializeField] private float continentalnessCenter = 0.5f;

        [Tooltip("Preferred erosion value (0=low erosion/peaks, 1=high erosion/flat)")]
        [Range(0f, 1f)]
        [SerializeField] private float erosionCenter = 0.5f;

        [Tooltip("Target surface height relative to sea level (blended per-biome)")]
        [SerializeField] private float baseHeight = 4f;

        [Tooltip("Terrain noise amplitude scale for this biome")]
        [SerializeField] private float heightAmplitude = 12f;

        [Header("Map")]
        [Tooltip("Color shown on the world map")]
        [SerializeField] private Color mapColor = Color.green;

        public string Namespace
        {
            get { return _namespace; }
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

        public float HeightModifier
        {
            get { return heightModifier; }
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
