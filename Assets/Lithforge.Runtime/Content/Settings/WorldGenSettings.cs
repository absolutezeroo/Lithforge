using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Lithforge/Settings/World Gen", order = 0)]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Terrain Noise")]
        [SerializeField] private NoiseLayerConfig terrainNoise = new NoiseLayerConfig
        {
            frequency = 0.008f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 5,
            seedOffset = 0,
        };

        [Header("Temperature Noise")]
        [SerializeField] private NoiseLayerConfig temperatureNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 999,
        };

        [Header("Humidity Noise")]
        [SerializeField] private NoiseLayerConfig humidityNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 1999,
        };

        [Header("Continentalness Noise")]
        [SerializeField] private NoiseLayerConfig continentalnessNoise = new NoiseLayerConfig
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.55f,
            heightScale = 1.0f,
            octaves = 4,
            seedOffset = 2999,
        };

        [Header("Erosion Noise")]
        [SerializeField] private NoiseLayerConfig erosionNoise = new NoiseLayerConfig
        {
            frequency = 0.003f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 3999,
        };

        [Header("Cave Noise")]
        [SerializeField] private NoiseLayerConfig caveNoise = new NoiseLayerConfig
        {
            frequency = 0.03f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 2,
            seedOffset = 0,
        };

        [Header("Cave Carving")]
        [Tooltip("Squared noise threshold for cave carving (smaller = fewer caves)")]
        [Min(0.001f)]
        [SerializeField] private float caveThreshold = 0.03f;

        [Tooltip("Minimum world-Y below which caves are not carved")]
        [SerializeField] private int minCarveY = 5;

        [Tooltip("First cave noise layer seed offset")]
        [SerializeField] private int caveSeedOffset1 = 0;

        [Tooltip("Second cave noise layer seed offset")]
        [SerializeField] private int caveSeedOffset2 = 31337;

        [Tooltip("Y buffer below sea level that prevents cave carving")]
        [SerializeField] private int seaLevelCarveBuffer = 4;

        [Header("World")]
        [Tooltip("World generation seed")]
        [SerializeField] private long seed = 42L;

        [Tooltip("Sea level in world Y coordinates")]
        [SerializeField] private int seaLevel = 64;

        [Header("Height Curve")]
        [Tooltip("Custom height distribution curve (optional)")]
        [SerializeField] private AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public NoiseLayerConfig TerrainNoise
        {
            get { return terrainNoise; }
        }

        public NoiseLayerConfig TemperatureNoise
        {
            get { return temperatureNoise; }
        }

        public NoiseLayerConfig HumidityNoise
        {
            get { return humidityNoise; }
        }

        public NoiseLayerConfig ContinentalnessNoise
        {
            get { return continentalnessNoise; }
        }

        public NoiseLayerConfig ErosionNoise
        {
            get { return erosionNoise; }
        }

        public NoiseLayerConfig CaveNoise
        {
            get { return caveNoise; }
        }

        public float CaveThreshold
        {
            get { return caveThreshold; }
        }

        public int MinCarveY
        {
            get { return minCarveY; }
        }

        public int CaveSeedOffset1
        {
            get { return caveSeedOffset1; }
        }

        public int CaveSeedOffset2
        {
            get { return caveSeedOffset2; }
        }

        public int SeaLevelCarveBuffer
        {
            get { return seaLevelCarveBuffer; }
        }

        public long Seed
        {
            get { return seed; }
        }

        public int SeaLevel
        {
            get { return seaLevel; }
        }

        public AnimationCurve HeightCurve
        {
            get { return heightCurve; }
        }
    }
}
