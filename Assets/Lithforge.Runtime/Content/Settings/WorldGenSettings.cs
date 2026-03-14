using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Lithforge/Settings/World Gen", order = 0)]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Terrain Noise")]
        [FormerlySerializedAs("terrainNoise")]
        [SerializeField] private NoiseLayerConfig _terrainNoise = new NoiseLayerConfig
        {
            frequency = 0.008f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 5,
            seedOffset = 0,
        };

        [Header("Temperature Noise")]
        [FormerlySerializedAs("temperatureNoise")]
        [SerializeField] private NoiseLayerConfig _temperatureNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 999,
        };

        [Header("Humidity Noise")]
        [FormerlySerializedAs("humidityNoise")]
        [SerializeField] private NoiseLayerConfig _humidityNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 1999,
        };

        [Header("Continentalness Noise")]
        [FormerlySerializedAs("continentalnessNoise")]
        [SerializeField] private NoiseLayerConfig _continentalnessNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.55f,
            heightScale = 1.0f,
            octaves = 4,
            seedOffset = 2999,
        };

        [Header("Erosion Noise")]
        [FormerlySerializedAs("erosionNoise")]
        [SerializeField] private NoiseLayerConfig _erosionNoise = new NoiseLayerConfig
        {
            frequency = 0.003f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 3999,
        };

        [Header("Cave Noise")]
        [FormerlySerializedAs("caveNoise")]
        [SerializeField] private NoiseLayerConfig _caveNoise = new NoiseLayerConfig
        {
            frequency = 0.03f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 2,
            seedOffset = 0,
        };

        [Header("River Noise")]
        [SerializeField] private RiverNoiseConfig _riverNoise = new RiverNoiseConfig
        {
            frequency = 0.003f,
            warpFrequency = 0.006f,
            warpStrength = 80f,
            baseThreshold = 0.04f,
            seedOffset = 7777,
            oceanContinentalnessCutoff = 0.3f,
            maxCarveDepthPlains = 4f,
            maxCarveDepthMountain = 22f,
        };

        [Header("Cave Carving")]
        [Tooltip("Squared noise threshold for cave carving (smaller = fewer caves)")]
        [Min(0.001f)]
        [FormerlySerializedAs("caveThreshold")]
        [SerializeField] private float _caveThreshold = 0.03f;

        [Tooltip("Minimum world-Y below which caves are not carved")]
        [FormerlySerializedAs("minCarveY")]
        [SerializeField] private int _minCarveY = 5;

        [Tooltip("First cave noise layer seed offset")]
        [FormerlySerializedAs("caveSeedOffset1")]
        [SerializeField] private int _caveSeedOffset1 = 0;

        [Tooltip("Second cave noise layer seed offset")]
        [FormerlySerializedAs("caveSeedOffset2")]
        [SerializeField] private int _caveSeedOffset2 = 31337;

        [Tooltip("Y buffer below sea level that prevents cave carving")]
        [FormerlySerializedAs("seaLevelCarveBuffer")]
        [SerializeField] private int _seaLevelCarveBuffer = 4;

        [Header("World")]
        [Tooltip("World generation seed")]
        [FormerlySerializedAs("seed")]
        [SerializeField] private long _seed = 42L;

        [Tooltip("Sea level in world Y coordinates")]
        [FormerlySerializedAs("seaLevel")]
        [SerializeField] private int _seaLevel = 64;

        [Header("Height Curve")]
        [Tooltip("Custom height distribution curve (optional)")]
        [FormerlySerializedAs("heightCurve")]
        [SerializeField] private AnimationCurve _heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public NoiseLayerConfig TerrainNoise
        {
            get { return _terrainNoise; }
        }

        public NoiseLayerConfig TemperatureNoise
        {
            get { return _temperatureNoise; }
        }

        public NoiseLayerConfig HumidityNoise
        {
            get { return _humidityNoise; }
        }

        public NoiseLayerConfig ContinentalnessNoise
        {
            get { return _continentalnessNoise; }
        }

        public NoiseLayerConfig ErosionNoise
        {
            get { return _erosionNoise; }
        }

        public NoiseLayerConfig CaveNoise
        {
            get { return _caveNoise; }
        }

        public RiverNoiseConfig RiverNoise
        {
            get { return _riverNoise; }
        }

        public float CaveThreshold
        {
            get { return _caveThreshold; }
        }

        public int MinCarveY
        {
            get { return _minCarveY; }
        }

        public int CaveSeedOffset1
        {
            get { return _caveSeedOffset1; }
        }

        public int CaveSeedOffset2
        {
            get { return _caveSeedOffset2; }
        }

        public int SeaLevelCarveBuffer
        {
            get { return _seaLevelCarveBuffer; }
        }

        public long Seed
        {
            get { return _seed; }
        }

        public int SeaLevel
        {
            get { return _seaLevel; }
        }

        public AnimationCurve HeightCurve
        {
            get { return _heightCurve; }
        }
    }
}
