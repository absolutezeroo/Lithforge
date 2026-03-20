using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     Loads all settings ScriptableObjects from <c>Resources/Settings/</c> at startup,
    ///     falling back to transient default instances when an asset is missing.
    /// </summary>
    public static class SettingsLoader
    {
        /// <summary>
        ///     Loads every settings asset and returns them bundled in a single <see cref="LoadedSettings" />.
        ///     Missing assets produce a log warning and a runtime-created default instance.
        /// </summary>
        /// <param name="logger">Logger for reporting missing assets.</param>
        /// <returns>A fully populated settings container, never null.</returns>
        public static LoadedSettings Load(ILogger logger)
        {
            LoadedSettings result = new()
            {
                WorldGen = LoadOrCreate<WorldGenSettings>("Settings/WorldGenSettings", logger),
                Chunk = LoadOrCreate<ChunkSettings>("Settings/ChunkSettings", logger),
                Physics = LoadOrCreate<PhysicsSettings>("Settings/PhysicsSettings", logger),
                Rendering = LoadOrCreate<RenderingSettings>("Settings/RenderingSettings", logger),
                Debug = LoadOrCreate<DebugSettings>("Settings/DebugSettings", logger),
                Gameplay = LoadOrCreate<GameplaySettings>("Settings/GameplaySettings", logger),
                Audio = LoadOrCreate<AudioSettings>("Settings/AudioSettings", logger),
            };

            return result;
        }

        /// <summary>
        ///     Attempts to load a ScriptableObject from the given Resources path; if the asset is
        ///     absent, creates a temporary instance with default field values so the game can still run.
        /// </summary>
        /// <param name="path">Resources-relative path without extension (e.g. "Settings/ChunkSettings").</param>
        /// <param name="logger">Logger for reporting missing assets.</param>
        /// <typeparam name="T">The ScriptableObject subclass to load.</typeparam>
        /// <returns>The loaded or freshly created asset, never null.</returns>
        private static T LoadOrCreate<T>(string path, ILogger logger) where T : ScriptableObject
        {
            T asset = Resources.Load<T>(path);

            if (asset == null)
            {
                logger.LogWarning(
                    $"[Lithforge] Settings asset not found at Resources/{path}. " +
                    "Using default values from a transient instance.");

                asset = ScriptableObject.CreateInstance<T>();
            }

            return asset;
        }
    }
}
