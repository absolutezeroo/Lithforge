namespace Lithforge.WorldGen.Biome
{
    /// <summary>Bit flags packed into <see cref="NativeBiomeData.SurfaceFlags"/> for fast Burst queries.</summary>
    public static class NativeBiomeSurfaceFlags
    {
        /// <summary>Biome is an ocean variant (suppresses trees, uses underwater surface blocks).</summary>
        public const byte IsOcean = 1;

        /// <summary>Biome has frozen water surfaces (ice replaces water at sea level).</summary>
        public const byte IsFrozen = 2;

        /// <summary>Biome is a beach transition zone (uses sand surface regardless of climate).</summary>
        public const byte IsBeach = 4;
    }
}
