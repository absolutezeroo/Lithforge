using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    public static class SettingsLoader
    {
        public static LoadedSettings Load()
        {
            LoadedSettings result = new LoadedSettings
            {
                WorldGen = LoadOrCreate<WorldGenSettings>("Settings/WorldGenSettings"),
                Chunk = LoadOrCreate<ChunkSettings>("Settings/ChunkSettings"),
                Physics = LoadOrCreate<PhysicsSettings>("Settings/PhysicsSettings"),
                Rendering = LoadOrCreate<RenderingSettings>("Settings/RenderingSettings"),
                Debug = LoadOrCreate<DebugSettings>("Settings/DebugSettings"),
                Gameplay = LoadOrCreate<GameplaySettings>("Settings/GameplaySettings"),
            };

            return result;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = Resources.Load<T>(path);

            if (asset == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Lithforge] Settings asset not found at Resources/{path}. " +
                    "Using default values from a transient instance.");

                asset = ScriptableObject.CreateInstance<T>();
            }

            return asset;
        }
    }
}
