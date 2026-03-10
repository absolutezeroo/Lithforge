using Lithforge.Runtime.Content.Settings;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Settings
{
    public static class SettingsAssetCreator
    {
        private const string _resourcesPath = "Assets/Resources/Settings";

        [MenuItem("Lithforge/Settings/Create All Settings Assets")]
        public static void CreateAll()
        {
            EnsureDirectory("Assets/Resources");
            EnsureDirectory(_resourcesPath);

            CreateIfMissing<WorldGenSettings>("WorldGenSettings");
            CreateIfMissing<ChunkSettings>("ChunkSettings");
            CreateIfMissing<PhysicsSettings>("PhysicsSettings");
            CreateIfMissing<RenderingSettings>("RenderingSettings");
            CreateIfMissing<DebugSettings>("DebugSettings");
            CreateIfMissing<GameplaySettings>("GameplaySettings");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log("[Lithforge] All settings assets created at " + _resourcesPath);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string folderName = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void CreateIfMissing<T>(string name) where T : ScriptableObject
        {
            string fullPath = _resourcesPath + "/" + name + ".asset";

            if (AssetDatabase.LoadAssetAtPath<T>(fullPath) != null)
            {
                UnityEngine.Debug.Log($"[Lithforge] Settings asset already exists: {fullPath}");

                return;
            }

            T instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, fullPath);
            UnityEngine.Debug.Log($"[Lithforge] Created: {fullPath}");
        }
    }
}
