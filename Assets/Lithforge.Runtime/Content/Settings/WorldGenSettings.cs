using Lithforge.WorldGen.Noise;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Lithforge/Settings/World Gen", order = 0)]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Terrain Noise")]
        [Min(0.0001f)]
        [Tooltip("Base frequency of terrain noise")]
        [SerializeField] private float _terrainFrequency = 0.008f;

        [Tooltip("Octave frequency multiplier")]
        [SerializeField] private float _terrainLacunarity = 2.0f;

        [Range(0f, 1f)]
        [Tooltip("Octave amplitude decay")]
        [SerializeField] private float _terrainPersistence = 0.5f;

        [Tooltip("Vertical scale applied to noise output")]
        [SerializeField] private float _terrainHeightScale = 24.0f;

        [Range(1, 8)]
        [Tooltip("Number of noise octaves")]
        [SerializeField] private int _terrainOctaves = 5;

        [Tooltip("Seed offset for terrain noise layer")]
        [SerializeField] private int _terrainSeedOffset = 0;

        [Header("Temperature Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _temperatureFrequency = 0.002f;

        [SerializeField] private float _temperatureLacunarity = 2.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _temperaturePersistence = 0.5f;

        [SerializeField] private float _temperatureHeightScale = 1.0f;

        [Range(1, 8)]
        [SerializeField] private int _temperatureOctaves = 3;

        [SerializeField] private int _temperatureSeedOffset = 999;

        [Header("Humidity Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _humidityFrequency = 0.002f;

        [SerializeField] private float _humidityLacunarity = 2.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _humidityPersistence = 0.5f;

        [SerializeField] private float _humidityHeightScale = 1.0f;

        [Range(1, 8)]
        [SerializeField] private int _humidityOctaves = 3;

        [SerializeField] private int _humiditySeedOffset = 1999;

        [Header("Cave Noise")]
        [Min(0.0001f)]
        [Tooltip("Base frequency of cave noise")]
        [SerializeField] private float _caveFrequency = 0.03f;

        [SerializeField] private float _caveLacunarity = 2.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _cavePersistence = 0.5f;

        [SerializeField] private float _caveHeightScale = 1.0f;

        [Range(1, 8)]
        [SerializeField] private int _caveOctaves = 2;

        [Header("Cave Carving")]
        [Tooltip("Squared noise threshold for cave carving (smaller = fewer caves)")]
        [Min(0.001f)]
        [SerializeField] private float _caveThreshold = 0.03f;

        [Tooltip("Minimum world-Y below which caves are not carved")]
        [SerializeField] private int _minCarveY = 5;

        [Tooltip("First cave noise layer seed offset")]
        [SerializeField] private int _caveSeedOffset1 = 0;

        [Tooltip("Second cave noise layer seed offset")]
        [SerializeField] private int _caveSeedOffset2 = 31337;

        [Tooltip("Y buffer below sea level that prevents cave carving")]
        [SerializeField] private int _seaLevelCarveBuffer = 4;

        [Header("World")]
        [Tooltip("World generation seed")]
        [SerializeField] private long _seed = 42L;

        [Tooltip("Sea level in world Y coordinates")]
        [SerializeField] private int _seaLevel = 64;

        [Header("Height Curve")]
        [Tooltip("Custom height distribution curve (optional)")]
        [SerializeField] private AnimationCurve _heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float TerrainFrequency
        {
            get { return _terrainFrequency; }
        }

        public float TerrainLacunarity
        {
            get { return _terrainLacunarity; }
        }

        public float TerrainPersistence
        {
            get { return _terrainPersistence; }
        }

        public float TerrainHeightScale
        {
            get { return _terrainHeightScale; }
        }

        public int TerrainOctaves
        {
            get { return _terrainOctaves; }
        }

        public int TerrainSeedOffset
        {
            get { return _terrainSeedOffset; }
        }

        public float TemperatureFrequency
        {
            get { return _temperatureFrequency; }
        }

        public float TemperatureLacunarity
        {
            get { return _temperatureLacunarity; }
        }

        public float TemperaturePersistence
        {
            get { return _temperaturePersistence; }
        }

        public float TemperatureHeightScale
        {
            get { return _temperatureHeightScale; }
        }

        public int TemperatureOctaves
        {
            get { return _temperatureOctaves; }
        }

        public int TemperatureSeedOffset
        {
            get { return _temperatureSeedOffset; }
        }

        public float HumidityFrequency
        {
            get { return _humidityFrequency; }
        }

        public float HumidityLacunarity
        {
            get { return _humidityLacunarity; }
        }

        public float HumidityPersistence
        {
            get { return _humidityPersistence; }
        }

        public float HumidityHeightScale
        {
            get { return _humidityHeightScale; }
        }

        public int HumidityOctaves
        {
            get { return _humidityOctaves; }
        }

        public int HumiditySeedOffset
        {
            get { return _humiditySeedOffset; }
        }

        public float CaveFrequency
        {
            get { return _caveFrequency; }
        }

        public int CaveOctaves
        {
            get { return _caveOctaves; }
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

        public NativeNoiseConfig BuildTerrainNoise()
        {
            return new NativeNoiseConfig
            {
                Frequency = _terrainFrequency,
                Lacunarity = _terrainLacunarity,
                Persistence = _terrainPersistence,
                HeightScale = _terrainHeightScale,
                Octaves = _terrainOctaves,
                SeedOffset = _terrainSeedOffset,
            };
        }

        public NativeNoiseConfig BuildTemperatureNoise()
        {
            return new NativeNoiseConfig
            {
                Frequency = _temperatureFrequency,
                Lacunarity = _temperatureLacunarity,
                Persistence = _temperaturePersistence,
                HeightScale = _temperatureHeightScale,
                Octaves = _temperatureOctaves,
                SeedOffset = _temperatureSeedOffset,
            };
        }

        public NativeNoiseConfig BuildHumidityNoise()
        {
            return new NativeNoiseConfig
            {
                Frequency = _humidityFrequency,
                Lacunarity = _humidityLacunarity,
                Persistence = _humidityPersistence,
                HeightScale = _humidityHeightScale,
                Octaves = _humidityOctaves,
                SeedOffset = _humiditySeedOffset,
            };
        }

        public NativeNoiseConfig BuildCaveNoise()
        {
            return new NativeNoiseConfig
            {
                Frequency = _caveFrequency,
                Lacunarity = _caveLacunarity,
                Persistence = _cavePersistence,
                HeightScale = _caveHeightScale,
                Octaves = _caveOctaves,
                SeedOffset = 0,
            };
        }
    }
}
