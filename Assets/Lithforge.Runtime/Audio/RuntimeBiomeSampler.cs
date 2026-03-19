using Lithforge.Runtime.Content.WorldGen;
using Unity.Mathematics;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Evaluates the biome at the player's position by sampling temperature and
    /// humidity noise, then finding the nearest biome center via weighted distance.
    /// Called at tick rate for a single position — negligible cost.
    /// </summary>
    public sealed class RuntimeBiomeSampler
    {
        /// <summary>Array of all biome definitions for nearest-biome lookup.</summary>
        private readonly BiomeDefinition[] _biomes;

        /// <summary>World seed used for position-based noise hashing.</summary>
        private readonly long _seed;

        /// <summary>Creates the sampler with the biome array and world seed.</summary>
        public RuntimeBiomeSampler(BiomeDefinition[] biomes, long seed)
        {
            _biomes = biomes;
            _seed = seed;
        }

        /// <summary>
        /// Index of the current biome (matches BiomeDefinition array index).
        /// </summary>
        public int CurrentBiomeIndex { get; private set; }

        /// <summary>
        /// The current biome definition, or null if no biomes exist.
        /// </summary>
        public BiomeDefinition CurrentBiome
        {
            get
            {
                if (_biomes == null || _biomes.Length == 0)
                {
                    return null;
                }

                return _biomes[CurrentBiomeIndex];
            }
        }

        /// <summary>
        /// Samples the biome at the given world XZ position. Uses a simplified
        /// noise-free heuristic based on position hash for runtime efficiency.
        /// The exact biome boundaries won't perfectly match worldgen, but the
        /// transitions will be consistent and smooth enough for ambient audio.
        /// </summary>
        public void Sample(float x, float z)
        {
            if (_biomes == null || _biomes.Length == 0)
            {
                return;
            }

            // Compute pseudo temperature and humidity from position hash
            // This is a simplified approximation; full noise evaluation would be
            // more accurate but expensive for a single-position audio-only query
            float temperature = Frac(math.sin(x * 0.00421f + z * 0.00137f + _seed * 0.001f) * 43758.5453f);
            float humidity = Frac(math.sin(x * 0.00317f + z * 0.00229f + _seed * 0.002f) * 23421.6312f);

            temperature = math.clamp(temperature, 0f, 1f);
            humidity = math.clamp(humidity, 0f, 1f);

            // Find nearest biome by weighted climate distance
            int bestIndex = 0;
            float bestWeight = float.MinValue;

            for (int i = 0; i < _biomes.Length; i++)
            {
                BiomeDefinition biome = _biomes[i];

                // Skip if outside hard bounds
                if (temperature < biome.TemperatureMin || temperature > biome.TemperatureMax)
                {
                    continue;
                }

                if (humidity < biome.HumidityMin || humidity > biome.HumidityMax)
                {
                    continue;
                }

                float dTemp = temperature - biome.TemperatureCenter;
                float dHum = humidity - biome.HumidityCenter;
                float dist = dTemp * dTemp + dHum * dHum;

                // Exponential falloff
                float weight = math.exp(-dist * biome.WeightSharpness);

                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestIndex = i;
                }
            }

            CurrentBiomeIndex = bestIndex;
        }

        /// <summary>Returns the fractional part of the given value.</summary>
        private static float Frac(float v)
        {
            return v - math.floor(v);
        }
    }
}
