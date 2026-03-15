namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Aggregates every settings ScriptableObject into a single pass-by-reference container,
    /// populated once at startup by <see cref="SettingsLoader.Load"/>.
    /// </summary>
    public sealed class LoadedSettings
    {
        /// <summary>Noise configs, sea level, seeds, and cave/river parameters for world generation.</summary>
        public WorldGenSettings WorldGen;

        /// <summary>Render distance, pool sizes, per-frame budgets, and spawn tuning.</summary>
        public ChunkSettings Chunk;

        /// <summary>Movement speeds, gravity, player dimensions, mining multipliers, and interaction range.</summary>
        public PhysicsSettings Physics;

        /// <summary>Materials, sky/fog gradients, camera clipping, day/night cycle, and atlas options.</summary>
        public RenderingSettings Rendering;

        /// <summary>Overlay visibility, profiling toggles, FPS smoothing, and benchmark configuration.</summary>
        public DebugSettings Debug;

        /// <summary>Inventory layout, crafting grid size, and items granted on first spawn.</summary>
        public GameplaySettings Gameplay;
    }
}
