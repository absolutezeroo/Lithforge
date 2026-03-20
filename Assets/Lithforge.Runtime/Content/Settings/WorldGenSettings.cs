using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     Noise parameters, cave carving thresholds, river shaping, sea level, seed, and height curve
    ///     that together define how terrain is procedurally generated.
    /// </summary>
    /// <remarks>
    ///     Each <see cref="NoiseLayerConfig" /> is baked into a <c>NativeNoiseConfig</c> at startup for
    ///     use in Burst-compiled generation jobs. Loaded from <c>Resources/Settings/WorldGenSettings</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Lithforge/Settings/World Gen", order = 0)]
    public sealed class WorldGenSettings : ScriptableObject
    {
        /// <summary>Primary terrain heightmap noise: controls broad hills, valleys, and plateaus.</summary>
        [Header("Terrain Noise"), SerializeField]
         private NoiseLayerConfig terrainNoise = new()
        {
            frequency = 0.008f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 5,
            seedOffset = 0,
        };

        /// <summary>Low-frequency noise that drives biome temperature; sampled per column.</summary>
        [Header("Temperature Noise"), SerializeField]
         private NoiseLayerConfig temperatureNoise = new()
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 999,
        };

        /// <summary>Low-frequency noise that drives biome humidity; sampled per column.</summary>
        [Header("Humidity Noise"), SerializeField]
         private NoiseLayerConfig humidityNoise = new()
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 1999,
        };

        /// <summary>Noise controlling land-vs-ocean distribution; low values produce ocean biomes.</summary>
        [Header("Continentalness Noise"), SerializeField]
         private NoiseLayerConfig continentalnessNoise = new()
        {
            frequency = 0.002f,
            lacunarity = 2.0f,
            persistence = 0.55f,
            heightScale = 1.0f,
            octaves = 4,
            seedOffset = 2999,
        };

        /// <summary>Noise that modifies terrain smoothness; high erosion produces flatter terrain.</summary>
        [Header("Erosion Noise"), SerializeField]
         private NoiseLayerConfig erosionNoise = new()
        {
            frequency = 0.003f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 3,
            seedOffset = 3999,
        };

        /// <summary>3D noise sampled by CaveCarverJob to carve spaghetti-style cave tunnels.</summary>
        [Header("Cave Noise"), SerializeField]
         private NoiseLayerConfig caveNoise = new()
        {
            frequency = 0.03f,
            lacunarity = 2.0f,
            persistence = 0.5f,
            heightScale = 1.0f,
            octaves = 2,
            seedOffset = 0,
        };

        /// <summary>Noise and carving parameters that shape river channels between landmasses.</summary>
        [Header("River Noise"), SerializeField]
         private RiverNoiseConfig riverNoise = new()
        {
            frequency = 0.003f,
            warpFrequency = 0.006f,
            warpStrength = 80f,
            baseThreshold = 0.065f,
            seedOffset = 7777,
            oceanContinentalnessCutoff = 0.3f,
            maxCarveDepthPlains = 4f,
            maxCarveDepthMountain = 22f,
        };

        /// <summary>Squared noise threshold below which voxels are carved into air (smaller = rarer caves).</summary>
        [Header("Cave Carving"), Tooltip("Squared noise threshold for cave carving (smaller = fewer caves)"), Min(0.001f), SerializeField]
         private float caveThreshold = 0.03f;

        /// <summary>World Y floor for cave carving; blocks at or below this level are never hollowed out.</summary>
        [Tooltip("Minimum world-Y below which caves are not carved"), SerializeField]
         private int minCarveY = 5;

        /// <summary>Seed offset for the first of the two 3D noise samples used in spaghetti cave carving.</summary>
        [Tooltip("First cave noise layer seed offset"), SerializeField]
         private int caveSeedOffset1;

        /// <summary>Seed offset for the second of the two 3D noise samples used in spaghetti cave carving.</summary>
        [Tooltip("Second cave noise layer seed offset"), SerializeField]
         private int caveSeedOffset2 = 31337;

        /// <summary>Vertical buffer below sea level where cave carving is suppressed to prevent ocean leaks.</summary>
        [Tooltip("Y buffer below sea level that prevents cave carving"), SerializeField]
         private int seaLevelCarveBuffer = 4;

        /// <summary>Master seed from which all per-noise and per-ore seeds are derived.</summary>
        [Header("World"), Tooltip("World generation seed"), SerializeField]
         private long seed = 42L;

        /// <summary>World Y coordinate at which water fills empty space; also the base for height remapping.</summary>
        [Tooltip("Sea level in world Y coordinates"), SerializeField]
         private int seaLevel = 64;

        /// <summary>Remaps the 0-1 noise output to a height multiplier; use an S-curve to flatten oceans and sharpen peaks.</summary>
        [Header("Height Curve"), Tooltip("Custom height distribution curve (optional)"), SerializeField]
         private AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        /// <inheritdoc cref="terrainNoise" />
        public NoiseLayerConfig TerrainNoise
        {
            get { return terrainNoise; }
        }

        /// <inheritdoc cref="temperatureNoise" />
        public NoiseLayerConfig TemperatureNoise
        {
            get { return temperatureNoise; }
        }

        /// <inheritdoc cref="humidityNoise" />
        public NoiseLayerConfig HumidityNoise
        {
            get { return humidityNoise; }
        }

        /// <inheritdoc cref="continentalnessNoise" />
        public NoiseLayerConfig ContinentalnessNoise
        {
            get { return continentalnessNoise; }
        }

        /// <inheritdoc cref="erosionNoise" />
        public NoiseLayerConfig ErosionNoise
        {
            get { return erosionNoise; }
        }

        /// <inheritdoc cref="caveNoise" />
        public NoiseLayerConfig CaveNoise
        {
            get { return caveNoise; }
        }

        /// <inheritdoc cref="riverNoise" />
        public RiverNoiseConfig RiverNoise
        {
            get { return riverNoise; }
        }

        /// <inheritdoc cref="caveThreshold" />
        public float CaveThreshold
        {
            get { return caveThreshold; }
        }

        /// <inheritdoc cref="minCarveY" />
        public int MinCarveY
        {
            get { return minCarveY; }
        }

        /// <inheritdoc cref="caveSeedOffset1" />
        public int CaveSeedOffset1
        {
            get { return caveSeedOffset1; }
        }

        /// <inheritdoc cref="caveSeedOffset2" />
        public int CaveSeedOffset2
        {
            get { return caveSeedOffset2; }
        }

        /// <inheritdoc cref="seaLevelCarveBuffer" />
        public int SeaLevelCarveBuffer
        {
            get { return seaLevelCarveBuffer; }
        }

        /// <inheritdoc cref="seed" />
        public long Seed
        {
            get { return seed; }
        }

        /// <inheritdoc cref="seaLevel" />
        public int SeaLevel
        {
            get { return seaLevel; }
        }

        /// <inheritdoc cref="heightCurve" />
        public AnimationCurve HeightCurve
        {
            get { return heightCurve; }
        }
    }
}
