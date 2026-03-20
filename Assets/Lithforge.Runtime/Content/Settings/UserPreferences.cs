using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

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

        /// <summary>Preferred screen resolution width in pixels, or -1 if unset.</summary>
        public int ResolutionWidth { get; set; } = -1;

        /// <summary>Preferred screen resolution height in pixels, or -1 if unset.</summary>
        public int ResolutionHeight { get; set; } = -1;

        /// <summary>Preferred fullscreen mode (int cast of FullScreenMode), or -1 if unset.</summary>
        public int FullScreenMode { get; set; } = -1;

        /// <summary>VSync count (0=off, 1=every vblank), or -1 if unset.</summary>
        public int VSyncCount { get; set; } = -1;

        /// <summary>Target frame rate (0=unlimited, 30/60/120/144/240), or -1 if unset.</summary>
        public int MaxFrameRate { get; set; } = -1;

        /// <summary>Shadow quality level (0=off, 1=low, 2=med, 3=high), or -1 if unset.</summary>
        public int ShadowQuality { get; set; } = -1;

        /// <summary>MSAA sample count (1=off, 2, 4, 8), or -1 if unset.</summary>
        public int MsaaLevel { get; set; } = -1;

        /// <summary>Render scale factor [0.5..1.0], or -1 if unset.</summary>
        public float RenderScale { get; set; } = -1f;

        /// <summary>Fog density multiplier, or -1 if unset.</summary>
        public float FogDensity { get; set; } = -1f;

        /// <summary>Mipmap streaming level (0-4), or -1 if unset.</summary>
        public int MipmapLevel { get; set; } = -1;

        /// <summary>Smooth lighting / AO toggle (0=off, 1=on), or -1 if unset.</summary>
        public int SmoothLighting { get; set; } = -1;

        /// <summary>Cloud quality (0=off, 1=fast, 2=fancy), or -1 if unset.</summary>
        public int CloudQuality { get; set; } = -1;

        /// <summary>Particle quality (0=all, 1=decreased, 2=minimal), or -1 if unset.</summary>
        public int ParticleQuality { get; set; } = -1;

        /// <summary>GUI scale (0=auto, 1-4), or -1 if unset.</summary>
        public int GuiScale { get; set; } = -1;

        /// <summary>Serialized key bindings JSON, or null if using defaults.</summary>
        public string KeyBindingsJson { get; set; }

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

        /// <summary>True if the user has explicitly set a resolution preference.</summary>
        public bool HasResolution
        {
            get { return ResolutionWidth >= 0 && ResolutionHeight >= 0; }
        }

        /// <summary>True if the user has explicitly set a fullscreen mode preference.</summary>
        public bool HasFullScreenMode
        {
            get { return FullScreenMode >= 0; }
        }

        /// <summary>True if the user has explicitly set a VSync preference.</summary>
        public bool HasVSyncCount
        {
            get { return VSyncCount >= 0; }
        }

        /// <summary>True if the user has explicitly set a max frame rate preference.</summary>
        public bool HasMaxFrameRate
        {
            get { return MaxFrameRate >= 0; }
        }

        /// <summary>True if the user has explicitly set a shadow quality preference.</summary>
        public bool HasShadowQuality
        {
            get { return ShadowQuality >= 0; }
        }

        /// <summary>True if the user has explicitly set an MSAA level preference.</summary>
        public bool HasMsaaLevel
        {
            get { return MsaaLevel >= 0; }
        }

        /// <summary>True if the user has explicitly set a render scale preference.</summary>
        public bool HasRenderScale
        {
            get { return RenderScale >= 0f; }
        }

        /// <summary>True if the user has explicitly set a fog density preference.</summary>
        public bool HasFogDensity
        {
            get { return FogDensity >= 0f; }
        }

        /// <summary>True if the user has explicitly set a mipmap level preference.</summary>
        public bool HasMipmapLevel
        {
            get { return MipmapLevel >= 0; }
        }

        /// <summary>True if the user has explicitly set a smooth lighting preference.</summary>
        public bool HasSmoothLighting
        {
            get { return SmoothLighting >= 0; }
        }

        /// <summary>True if the user has explicitly set a cloud quality preference.</summary>
        public bool HasCloudQuality
        {
            get { return CloudQuality >= 0; }
        }

        /// <summary>True if the user has explicitly set a particle quality preference.</summary>
        public bool HasParticleQuality
        {
            get { return ParticleQuality >= 0; }
        }

        /// <summary>True if the user has explicitly set a GUI scale preference.</summary>
        public bool HasGuiScale
        {
            get { return GuiScale >= 0; }
        }

        /// <summary>True if the user has explicitly set key bindings.</summary>
        public bool HasKeyBindings
        {
            get { return KeyBindingsJson is not null; }
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
                ["resolution_width"] = ResolutionWidth,
                ["resolution_height"] = ResolutionHeight,
                ["fullscreen_mode"] = FullScreenMode,
                ["vsync_count"] = VSyncCount,
                ["max_frame_rate"] = MaxFrameRate,
                ["shadow_quality"] = ShadowQuality,
                ["msaa_level"] = MsaaLevel,
                ["render_scale"] = RenderScale,
                ["fog_density"] = FogDensity,
                ["mipmap_level"] = MipmapLevel,
                ["smooth_lighting"] = SmoothLighting,
                ["cloud_quality"] = CloudQuality,
                ["particle_quality"] = ParticleQuality,
                ["gui_scale"] = GuiScale,
            };

            if (KeyBindingsJson is not null)
            {
                root["key_bindings"] = KeyBindingsJson;
            }

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
        public static UserPreferences Load(ILogger logger = null)
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
                        ResolutionWidth = root["resolution_width"]?.Value<int>() ?? -1,
                        ResolutionHeight = root["resolution_height"]?.Value<int>() ?? -1,
                        FullScreenMode = root["fullscreen_mode"]?.Value<int>() ?? -1,
                        VSyncCount = root["vsync_count"]?.Value<int>() ?? -1,
                        MaxFrameRate = root["max_frame_rate"]?.Value<int>() ?? -1,
                        ShadowQuality = root["shadow_quality"]?.Value<int>() ?? -1,
                        MsaaLevel = root["msaa_level"]?.Value<int>() ?? -1,
                        RenderScale = root["render_scale"]?.Value<float>() ?? -1f,
                        FogDensity = root["fog_density"]?.Value<float>() ?? -1f,
                        MipmapLevel = root["mipmap_level"]?.Value<int>() ?? -1,
                        SmoothLighting = root["smooth_lighting"]?.Value<int>() ?? -1,
                        CloudQuality = root["cloud_quality"]?.Value<int>() ?? -1,
                        ParticleQuality = root["particle_quality"]?.Value<int>() ?? -1,
                        GuiScale = root["gui_scale"]?.Value<int>() ?? -1,
                        KeyBindingsJson = root["key_bindings"]?.Value<string>(),
                    };

                    return prefs;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
                        $"[UserPreferences] Failed to load {filePath}: {ex.Message}. Using defaults.");

                    return new UserPreferences();
                }
            }

            // No JSON file — attempt one-time migration from PlayerPrefs
            return MigrateFromPlayerPrefs(logger);
        }

        /// <summary>
        ///     Reads legacy PlayerPrefs keys, creates a UserPreferences, saves it to JSON,
        ///     then deletes the old PlayerPrefs keys. Returns defaults if no legacy keys exist.
        /// </summary>
        private static UserPreferences MigrateFromPlayerPrefs(ILogger logger = null)
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

                    logger?.LogInfo(
                        "[UserPreferences] Migrated settings from PlayerPrefs to preferences.json.");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
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
