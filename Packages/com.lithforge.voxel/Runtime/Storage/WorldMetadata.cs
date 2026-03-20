using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     World metadata stored in world.json at the root of each world directory.
    ///     Contains seed, game mode, timestamps, and content hash.
    ///     Player state is stored separately in playerdata/ via <see cref="PlayerDataStore"/>.
    ///     Saved atomically via temp-file + rename pattern.
    /// </summary>
    public sealed class WorldMetadata
    {
        /// <summary>Human-readable world name shown in the world selection UI.</summary>
        public string DisplayName { get; set; } = "New World";

        /// <summary>World generation seed.</summary>
        public long Seed { get; set; }

        /// <summary>Game mode (Survival or Creative).</summary>
        public GameMode GameMode { get; set; } = GameMode.Survival;

        /// <summary>UTC timestamp when the world was first created.</summary>
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the most recent play session (updated on every save).</summary>
        public DateTime LastPlayed { get; set; } = DateTime.UtcNow;

        /// <summary>Save format version for migration compatibility.</summary>
        public int DataVersion { get; set; } = 3;

        /// <summary>Hash of the content pipeline output, used to detect content pack changes.</summary>
        public string ContentHash { get; set; } = "";

        /// <summary>
        ///     Serializes this metadata to a JSON file at the given path.
        ///     Uses atomic write (temp file + rename) to prevent corruption.
        /// </summary>
        public void Save(string filePath)
        {
            LastPlayed = DateTime.UtcNow;

            JObject root = new()
            {
                ["display_name"] = DisplayName,
                ["seed"] = Seed,
                ["game_mode"] = (int)GameMode,
                ["creation_date"] = CreationDate.ToString("o"),
                ["last_played"] = LastPlayed.ToString("o"),
                ["data_version"] = DataVersion,
                ["content_hash"] = ContentHash,
            };

            string dir = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Atomic write: write to .tmp then rename
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
        ///     Loads metadata from a world.json file. Returns null if the file
        ///     does not exist or parsing fails. Supports both v1 and v2 format fields.
        /// </summary>
        public static WorldMetadata Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                WorldMetadata meta = new()
                {
                    Seed = root["seed"]?.Value<long>() ?? 0,
                    DataVersion = root["data_version"]?.Value<int>() ?? root["version"]?.Value<int>() ?? 1,
                    ContentHash = root["content_hash"]?.Value<string>() ?? "",
                    DisplayName = root["display_name"]?.Value<string>() ?? "",
                    GameMode = (GameMode)(root["game_mode"]?.Value<int>() ?? 0),
                };

                string creationStr = root["creation_date"]?.Value<string>();

                if (!string.IsNullOrEmpty(creationStr) && DateTime.TryParse(creationStr, out DateTime creationParsed))
                {
                    meta.CreationDate = creationParsed;
                }

                string lastPlayedStr = root["last_played"]?.Value<string>() ?? root["last_saved"]?.Value<string>();

                if (!string.IsNullOrEmpty(lastPlayedStr) && DateTime.TryParse(lastPlayedStr, out DateTime lastParsed))
                {
                    meta.LastPlayed = lastParsed;
                }

                return meta;
            }
            catch
            {
                return null;
            }
        }
    }
}
