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

        [FormerlySerializedAs("biomeName")]
        [Tooltip("Biome name")]
        [SerializeField] private string _biomeName = "";

        [Header("Climate Range")]
        [FormerlySerializedAs("temperatureMin")]
        [Tooltip("Minimum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureMin;

        [FormerlySerializedAs("temperatureMax")]
        [Tooltip("Maximum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureMax = 1.0f;

        [FormerlySerializedAs("temperatureCenter")]
        [Tooltip("Preferred temperature center")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureCenter = 0.5f;

        [FormerlySerializedAs("humidityMin")]
        [Tooltip("Minimum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityMin;

        [FormerlySerializedAs("humidityMax")]
        [Tooltip("Maximum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityMax = 1.0f;

        [FormerlySerializedAs("humidityCenter")]
        [Tooltip("Preferred humidity center")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityCenter = 0.5f;

        [Header("Surface Blocks")]
        [FormerlySerializedAs("topBlock")]
        [Tooltip("Top surface block (e.g. grass_block)")]
        [SerializeField] private BlockDefinition _topBlock;

        [FormerlySerializedAs("fillerBlock")]
        [Tooltip("Filler block below surface (e.g. dirt)")]
        [SerializeField] private BlockDefinition _fillerBlock;

        [FormerlySerializedAs("stoneBlock")]
        [Tooltip("Stone block")]
        [SerializeField] private BlockDefinition _stoneBlock;

        [FormerlySerializedAs("underwaterBlock")]
        [Tooltip("Block used underwater")]
        [SerializeField] private BlockDefinition _underwaterBlock;

        [Header("Terrain")]
        [FormerlySerializedAs("fillerDepth")]
        [Tooltip("Depth of filler blocks")]
        [Min(0)]
        [SerializeField] private int _fillerDepth = 3;

        [FormerlySerializedAs("treeDensity")]
        [Tooltip("Tree density (0 = no trees, 1 = maximum)")]
        [Range(0f, 1f)]
        [SerializeField] private float _treeDensity;

        [FormerlySerializedAs("treeType")]
        [Tooltip("Tree shape variant for this biome (0=oak, 1=birch, 2=spruce)")]
        [Range(0, 2)]
        [SerializeField] private int _treeType;

        [Header("Extended Climate")]
        [FormerlySerializedAs("continentalnessCenter")]
        [Tooltip("Preferred continentalness value (0=ocean, 1=far inland)")]
        [Range(0f, 1f)]
        [SerializeField] private float _continentalnessCenter = 0.5f;

        [FormerlySerializedAs("erosionCenter")]
        [Tooltip("Preferred erosion value (0=low erosion/peaks, 1=high erosion/flat)")]
        [Range(0f, 1f)]
        [SerializeField] private float _erosionCenter = 0.5f;

        [FormerlySerializedAs("baseHeight")]
        [Tooltip("Target surface height relative to sea level (blended per-biome)")]
        [SerializeField] private float _baseHeight = 4f;

        [FormerlySerializedAs("heightAmplitude")]
        [Tooltip("Terrain noise amplitude scale for this biome")]
        [SerializeField] private float _heightAmplitude = 12f;

        [Header("Tinting")]
        [FormerlySerializedAs("waterColor")]
        [Tooltip("Water tint color for this biome")]
        [SerializeField] private Color _waterColor = new Color(0.247f, 0.463f, 0.894f, 1f);

        [Header("Map")]
        [FormerlySerializedAs("mapColor")]
        [Tooltip("Color shown on the world map")]
        [SerializeField] private Color _mapColor = Color.green;

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BiomeName
        {
            get { return _biomeName; }
        }

        public float TemperatureMin
        {
            get { return _temperatureMin; }
        }

        public float TemperatureMax
        {
            get { return _temperatureMax; }
        }

        public float TemperatureCenter
        {
            get { return _temperatureCenter; }
        }

        public float HumidityMin
        {
            get { return _humidityMin; }
        }

        public float HumidityMax
        {
            get { return _humidityMax; }
        }

        public float HumidityCenter
        {
            get { return _humidityCenter; }
        }

        public BlockDefinition TopBlock
        {
            get { return _topBlock; }
        }

        public BlockDefinition FillerBlock
        {
            get { return _fillerBlock; }
        }

        public BlockDefinition StoneBlock
        {
            get { return _stoneBlock; }
        }

        public BlockDefinition UnderwaterBlock
        {
            get { return _underwaterBlock; }
        }

        public int FillerDepth
        {
            get { return _fillerDepth; }
        }

        public float TreeDensity
        {
            get { return _treeDensity; }
        }

        public int TreeType
        {
            get { return _treeType; }
        }

        public float ContinentalnessCenter
        {
            get { return _continentalnessCenter; }
        }

        public float ErosionCenter
        {
            get { return _erosionCenter; }
        }

        public float BaseHeight
        {
            get { return _baseHeight; }
        }

        public float HeightAmplitude
        {
            get { return _heightAmplitude; }
        }

        public Color WaterColor
        {
            get { return _waterColor; }
        }

        public Color MapColor
        {
            get { return _mapColor; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_biomeName))
            {
                _biomeName = name;
            }
        }
    }
}
