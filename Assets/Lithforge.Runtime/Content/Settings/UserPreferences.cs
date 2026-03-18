using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     User preferences persisted as human-readable JSON at
    ///     Application.persistentDataPath/preferences.json.
    ///     Sentinel value -1 means "not explicitly set by user — use system default."
    ///     Follows the WorldMetadata.cs atomic-write pattern (.tmp → .bak → rename).
    /// </summary>
    public sealed class UserPreferences
    {
        private const int CurrentVersion = 1;
        private const string FileName = "preferences.json";

        // Legacy PlayerPrefs keys (for one-time migration)
        private const string LegacyPrefRenderDistance = "LF_RenderDistance";
        private const string LegacyPrefFOV = "LF_FOV";
        private const string LegacyPrefMouseSensitivity = "LF_MouseSensitivity";
        private const string LegacyPrefAOStrength = "LF_AOStrength";

        public int Version { get; set; } = CurrentVersion;
        public int RenderDistance { get; set; } = -1;
        public float FieldOfView { get; set; } = -1f;
        public float MouseSensitivity { get; set; } = -1f;
        public float AOStrength { get; set; } = -1f;
        public float MasterVolume { get; set; } = -1f;
        public float SfxVolume { get; set; } = -1f;
        public float MusicVolume { get; set; } = -1f;
        public float AmbientVolume { get; set; } = -1f;

        public bool HasRenderDistance
        {
            get { return RenderDistance >= 0; }
        }

        public bool HasFieldOfView
        {
            get { return FieldOfView >= 0f; }
        }

        public bool HasMouseSensitivity
        {
            get { return MouseSensitivity >= 0f; }
        }

        public bool HasAOStrength
        {
            get { return AOStrength >= 0f; }
        }

        public bool HasMasterVolume
        {
            get { return MasterVolume >= 0f; }
        }

        public bool HasSfxVolume
        {
            get { return SfxVolume >= 0f; }
        }

        public bool HasMusicVolume
        {
            get { return MusicVolume >= 0f; }
        }

        public bool HasAmbientVolume
        {
            get { return AmbientVolume >= 0f; }
        }

        /// <summary>
        ///     Persists preferences to disk using atomic write.
        /// </summary>
        public void Save()
        {
            string filePath = GetFilePath();

            JObject root = new()
            {
                ["version"] = Version,
                ["render_distance"] = RenderDistance,
                ["field_of_view"] = FieldOfView,
                ["mouse_sensitivity"] = MouseSensitivity,
                ["ao_strength"] = AOStrength,
                ["master_volume"] = MasterVolume,
                ["sfx_volume"] = SfxVolume,
                ["music_volume"] = MusicVolume,
                ["ambient_volume"] = AmbientVolume,
            };

            string dir = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Atomic write: .tmp → .bak → rename
            string tempPath = filePath + ".tmp";

            try
            {
                File.WriteAllText(tempPath, root.ToString(Formatting.Indented));

                if (File.Exists(filePath))
                {
                    string backupPath = filePath + ".bak";

                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }

                    File.Move(filePath, backupPath);
                }

                File.Move(tempPath, filePath);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }

                throw;
            }
        }

        /// <summary>
        ///     Loads preferences from disk. If no file exists, attempts one-time
        ///     migration from legacy PlayerPrefs keys. Returns a new instance with
        ///     sentinel defaults if nothing is found.
        /// </summary>
        public static UserPreferences Load()
        {
            string filePath = GetFilePath();

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    JObject root = JObject.Parse(json);

                    UserPreferences prefs = new()
                    {
                        Version = root["version"]?.Value<int>() ?? CurrentVersion,
                        RenderDistance = root["render_distance"]?.Value<int>() ?? -1,
                        FieldOfView = root["field_of_view"]?.Value<float>() ?? -1f,
                        MouseSensitivity = root["mouse_sensitivity"]?.Value<float>() ?? -1f,
                        AOStrength = root["ao_strength"]?.Value<float>() ?? -1f,
                        MasterVolume = root["master_volume"]?.Value<float>() ?? -1f,
                        SfxVolume = root["sfx_volume"]?.Value<float>() ?? -1f,
                        MusicVolume = root["music_volume"]?.Value<float>() ?? -1f,
                        AmbientVolume = root["ambient_volume"]?.Value<float>() ?? -1f,
                    };

                    return prefs;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[UserPreferences] Failed to load {filePath}: {ex.Message}. Using defaults.");

                    return new UserPreferences();
                }
            }

            // No JSON file — attempt one-time migration from PlayerPrefs
            return MigrateFromPlayerPrefs();
        }

        /// <summary>
        ///     Reads legacy PlayerPrefs keys, creates a UserPreferences, saves it to JSON,
        ///     then deletes the old PlayerPrefs keys. Returns defaults if no legacy keys exist.
        /// </summary>
        private static UserPreferences MigrateFromPlayerPrefs()
        {
            UserPreferences prefs = new();
            bool migrated = false;

            if (PlayerPrefs.HasKey(LegacyPrefRenderDistance))
            {
                prefs.RenderDistance = PlayerPrefs.GetInt(LegacyPrefRenderDistance);
                migrated = true;
            }

            if (PlayerPrefs.HasKey(LegacyPrefFOV))
            {
                prefs.FieldOfView = PlayerPrefs.GetFloat(LegacyPrefFOV);
                migrated = true;
            }

            if (PlayerPrefs.HasKey(LegacyPrefMouseSensitivity))
            {
                prefs.MouseSensitivity = PlayerPrefs.GetFloat(LegacyPrefMouseSensitivity);
                migrated = true;
            }

            if (PlayerPrefs.HasKey(LegacyPrefAOStrength))
            {
                prefs.AOStrength = PlayerPrefs.GetFloat(LegacyPrefAOStrength);
                migrated = true;
            }

            if (migrated)
            {
                try
                {
                    prefs.Save();

                    // Delete legacy keys after successful save
                    PlayerPrefs.DeleteKey(LegacyPrefRenderDistance);
                    PlayerPrefs.DeleteKey(LegacyPrefFOV);
                    PlayerPrefs.DeleteKey(LegacyPrefMouseSensitivity);
                    PlayerPrefs.DeleteKey(LegacyPrefAOStrength);
                    PlayerPrefs.Save();

                    UnityEngine.Debug.Log(
                        "[UserPreferences] Migrated settings from PlayerPrefs to preferences.json.");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[UserPreferences] Migration save failed: {ex.Message}. " +
                        "Legacy PlayerPrefs keys were NOT deleted.");
                }
            }

            return prefs;
        }

        private static string GetFilePath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }
}
