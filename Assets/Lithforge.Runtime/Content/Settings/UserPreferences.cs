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
        /// <summary>Current schema version for forward-compatible JSON deserialization.</summary>
        private const int CurrentVersion = 1;

        /// <summary>File name for the preferences JSON on disk.</summary>
        private const string FileName = "preferences.json";

        /// <summary>Legacy PlayerPrefs key for render distance (pre-JSON migration).</summary>
        private const string LegacyPrefRenderDistance = "LF_RenderDistance";

        /// <summary>Legacy PlayerPrefs key for field of view (pre-JSON migration).</summary>
        private const string LegacyPrefFOV = "LF_FOV";

        /// <summary>Legacy PlayerPrefs key for mouse sensitivity (pre-JSON migration).</summary>
        private const string LegacyPrefMouseSensitivity = "LF_MouseSensitivity";

        /// <summary>Legacy PlayerPrefs key for AO strength (pre-JSON migration).</summary>
        private const string LegacyPrefAOStrength = "LF_AOStrength";

        /// <summary>Schema version stored in the JSON for forward compatibility.</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Preferred render distance in chunks, or -1 if unset.</summary>
        public int RenderDistance { get; set; } = -1;

        /// <summary>Preferred camera field of view in degrees, or -1 if unset.</summary>
        public float FieldOfView { get; set; } = -1f;

        /// <summary>Preferred mouse sensitivity multiplier, or -1 if unset.</summary>
        public float MouseSensitivity { get; set; } = -1f;

        /// <summary>Preferred ambient occlusion strength [0..1], or -1 if unset.</summary>
        public float AOStrength { get; set; } = -1f;

        /// <summary>Preferred master volume [0..1], or -1 if unset.</summary>
        public float MasterVolume { get; set; } = -1f;

        /// <summary>Preferred SFX volume [0..1], or -1 if unset.</summary>
        public float SfxVolume { get; set; } = -1f;

        /// <summary>Preferred music volume [0..1], or -1 if unset.</summary>
        public float MusicVolume { get; set; } = -1f;

        /// <summary>Preferred ambient volume [0..1], or -1 if unset.</summary>
        public float AmbientVolume { get; set; } = -1f;

        /// <summary>True if the user has explicitly set a render distance preference.</summary>
        public bool HasRenderDistance
        {
            get { return RenderDistance >= 0; }
        }

        /// <summary>True if the user has explicitly set a field of view preference.</summary>
        public bool HasFieldOfView
        {
            get { return FieldOfView >= 0f; }
        }

        /// <summary>True if the user has explicitly set a mouse sensitivity preference.</summary>
        public bool HasMouseSensitivity
        {
            get { return MouseSensitivity >= 0f; }
        }

        /// <summary>True if the user has explicitly set an AO strength preference.</summary>
        public bool HasAOStrength
        {
            get { return AOStrength >= 0f; }
        }

        /// <summary>True if the user has explicitly set a master volume preference.</summary>
        public bool HasMasterVolume
        {
            get { return MasterVolume >= 0f; }
        }

        /// <summary>True if the user has explicitly set an SFX volume preference.</summary>
        public bool HasSfxVolume
        {
            get { return SfxVolume >= 0f; }
        }

        /// <summary>True if the user has explicitly set a music volume preference.</summary>
        public bool HasMusicVolume
        {
            get { return MusicVolume >= 0f; }
        }

        /// <summary>True if the user has explicitly set an ambient volume preference.</summary>
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

        /// <summary>Returns the full filesystem path for the preferences JSON file.</summary>
        private static string GetFilePath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }
}
