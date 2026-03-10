using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Lithforge/Settings/World Gen", order = 0)]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Terrain Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _terrainFrequency = 0.008f;

        [SerializeField] private float _terrainLacunarity = 2.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _terrainPersistence = 0.5f;

        [SerializeField] private float _terrainHeightScale = 24.0f;

        [Range(1, 8)]
        [SerializeField] private int _terrainOctaves = 5;

        [Header("Temperature Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _temperatureFrequency = 0.002f;

        [Range(1, 8)]
        [SerializeField] private int _temperatureOctaves = 3;

        [SerializeField] private int _temperatureSeedOffset = 999;

        [Header("Humidity Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _humidityFrequency = 0.002f;

        [Range(1, 8)]
        [SerializeField] private int _humidityOctaves = 3;

        [SerializeField] private int _humiditySeedOffset = 1999;

        [Header("Cave Noise")]
        [Min(0.0001f)]
        [SerializeField] private float _caveFrequency = 0.03f;

        [Range(1, 8)]
        [SerializeField] private int _caveOctaves = 2;

        [Header("Sea Level")]
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

        public float TemperatureFrequency
        {
            get { return _temperatureFrequency; }
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
