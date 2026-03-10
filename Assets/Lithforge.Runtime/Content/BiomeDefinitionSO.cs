using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBiome", menuName = "Lithforge/Content/Biome Definition", order = 7)]
    public sealed class BiomeDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Biome name")]
        [SerializeField] private string _biomeName = "";

        [Header("Climate Range")]
        [Tooltip("Minimum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureMin;

        [Tooltip("Maximum temperature")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureMax = 1.0f;

        [Tooltip("Preferred temperature center")]
        [Range(0f, 1f)]
        [SerializeField] private float _temperatureCenter = 0.5f;

        [Tooltip("Minimum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityMin;

        [Tooltip("Maximum humidity")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityMax = 1.0f;

        [Tooltip("Preferred humidity center")]
        [Range(0f, 1f)]
        [SerializeField] private float _humidityCenter = 0.5f;

        [Header("Surface Blocks")]
        [Tooltip("Top surface block (e.g. grass_block)")]
        [SerializeField] private BlockDefinitionSO _topBlock;

        [Tooltip("Filler block below surface (e.g. dirt)")]
        [SerializeField] private BlockDefinitionSO _fillerBlock;

        [Tooltip("Stone block")]
        [SerializeField] private BlockDefinitionSO _stoneBlock;

        [Tooltip("Block used underwater")]
        [SerializeField] private BlockDefinitionSO _underwaterBlock;

        [Header("Terrain")]
        [Tooltip("Depth of filler blocks")]
        [Min(0)]
        [SerializeField] private int _fillerDepth = 3;

        [Tooltip("Tree density (0 = no trees, 1 = maximum)")]
        [Range(0f, 1f)]
        [SerializeField] private float _treeDensity;

        [Tooltip("Height modifier for terrain generation")]
        [SerializeField] private float _heightModifier;

        [Header("Map")]
        [Tooltip("Color shown on the world map")]
        [SerializeField] private Color _mapColor = Color.green;

        public string Namespace
        {
            get { return _namespace; }
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

        public BlockDefinitionSO TopBlock
        {
            get { return _topBlock; }
        }

        public BlockDefinitionSO FillerBlock
        {
            get { return _fillerBlock; }
        }

        public BlockDefinitionSO StoneBlock
        {
            get { return _stoneBlock; }
        }

        public BlockDefinitionSO UnderwaterBlock
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

        public float HeightModifier
        {
            get { return _heightModifier; }
        }

        public Color MapColor
        {
            get { return _mapColor; }
        }
    }
}
