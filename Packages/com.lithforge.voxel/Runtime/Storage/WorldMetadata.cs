using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Storage
{
    public sealed class WorldMetadata
    {
        public long Seed { get; set; }
        public int Version { get; set; }
        public string ContentHash { get; set; }
        public DateTime LastSaved { get; set; }

        public WorldMetadata()
        {
            Version = 1;
            ContentHash = "";
            LastSaved = DateTime.UtcNow;
        }

        public void Save(string filePath)
        {
            LastSaved = DateTime.UtcNow;

            JObject root = new JObject
            {
                ["seed"] = Seed,
                ["version"] = Version,
                ["content_hash"] = ContentHash,
                ["last_saved"] = LastSaved.ToString("o"),
            };

            string dir = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, root.ToString(Formatting.Indented));
        }

        public static WorldMetadata Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = File.ReadAllText(filePath);
            JObject root = JObject.Parse(json);

            WorldMetadata meta = new WorldMetadata();
            meta.Seed = root["seed"]?.Value<long>() ?? 0;
            meta.Version = root["version"]?.Value<int>() ?? 1;
            meta.ContentHash = root["content_hash"]?.Value<string>() ?? "";

            string lastSavedStr = root["last_saved"]?.Value<string>();

            if (!string.IsNullOrEmpty(lastSavedStr) &&
                DateTime.TryParse(lastSavedStr, out DateTime parsed))
            {
                meta.LastSaved = parsed;
            }

            return meta;
        }
    }
}
