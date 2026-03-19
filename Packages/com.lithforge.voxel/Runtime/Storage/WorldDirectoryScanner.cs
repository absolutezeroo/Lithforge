using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Discovers, creates, and deletes world directories under the worlds root folder.
    ///     Each world is a subdirectory containing a world.json metadata file.
    /// </summary>
    public static class WorldDirectoryScanner
    {
        /// <summary>Maximum character length for sanitized world directory names.</summary>
        private const int MaxNameLength = 64;

        /// <summary>Regex matching characters not allowed in directory names.</summary>
        private static readonly Regex s_invalidChars = new(@"[^a-zA-Z0-9_\-]", RegexOptions.Compiled);

        /// <summary>
        ///     Scans the worlds root for subdirectories, reads their metadata, and returns
        ///     entries sorted by last-played date (most recent first).
        /// </summary>
        public static List<WorldScanEntry> ScanWorlds(string worldsRoot)
        {
            List<WorldScanEntry> entries = new();

            if (!Directory.Exists(worldsRoot))
            {
                return entries;
            }

            string[] dirs = Directory.GetDirectories(worldsRoot);

            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i];
                string dirName = Path.GetFileName(dir);
                string metaPath = Path.Combine(dir, "world.json");
                WorldMetadata metadata = null;

                if (File.Exists(metaPath))
                {
                    metadata = WorldMetadata.Load(metaPath);
                }

                // If metadata loaded but has no display name, use directory name
                if (metadata != null && string.IsNullOrEmpty(metadata.DisplayName))
                {
                    metadata.DisplayName = dirName;
                }

                bool isLocked = SessionLock.IsLocked(dir);

                entries.Add(new WorldScanEntry(dir, dirName, metadata, isLocked));
            }

            // Sort by last played (most recent first), nulls last
            entries.Sort((a, b) =>
            {
                if (a.Metadata == null && b.Metadata == null)
                {
                    return 0;
                }

                if (a.Metadata == null)
                {
                    return 1;
                }

                if (b.Metadata == null)
                {
                    return -1;
                }

                return b.Metadata.LastPlayed.CompareTo(a.Metadata.LastPlayed);
            });

            return entries;
        }

        /// <summary>
        ///     Creates a new world directory with initial metadata.
        ///     Returns the full path to the newly created world directory.
        /// </summary>
        public static string CreateWorld(string worldsRoot, string displayName, long seed, GameMode mode)
        {
            if (!Directory.Exists(worldsRoot))
            {
                Directory.CreateDirectory(worldsRoot);
            }

            string slug = SanitizeName(displayName);

            // Ensure uniqueness
            string worldDir = Path.Combine(worldsRoot, slug);
            int suffix = 2;

            while (Directory.Exists(worldDir))
            {
                worldDir = Path.Combine(worldsRoot, slug + "_" + suffix);
                suffix++;
            }

            Directory.CreateDirectory(worldDir);

            WorldMetadata meta = new()
            {
                DisplayName = displayName,
                Seed = seed,
                GameMode = mode,
                CreationDate = DateTime.UtcNow,
                LastPlayed = DateTime.UtcNow,
                DataVersion = 2,
            };

            meta.Save(Path.Combine(worldDir, "world.json"));

            return worldDir;
        }

        /// <summary>Recursively deletes the specified world directory and all its contents.</summary>
        public static void DeleteWorld(string worldDir)
        {
            if (Directory.Exists(worldDir))
            {
                Directory.Delete(worldDir, true);
            }
        }

        /// <summary>
        ///     Sanitizes a display name into a filesystem-safe directory slug.
        ///     Replaces spaces with underscores, strips invalid characters, and truncates to MaxNameLength.
        /// </summary>
        public static string SanitizeName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = "world";
            }

            // Replace spaces with underscores, strip invalid chars
            string sanitized = displayName.Replace(' ', '_');
            sanitized = s_invalidChars.Replace(sanitized, "");

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "world";
            }

            if (sanitized.Length > MaxNameLength)
            {
                sanitized = sanitized.Substring(0, MaxNameLength);
            }

            return sanitized;
        }
    }
}
