using System;
using System.IO;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Shared utility for recursive JSON file discovery in content directories.
    /// Scans a directory tree and invokes a callback for each JSON file found,
    /// providing the file path and computed ResourceId.
    /// </summary>
    internal static class ContentDirectoryScanner
    {
        /// <summary>
        /// Recursively scans a directory for *.json files, computing a ResourceId
        /// from the namespace and relative path prefix for each file found.
        /// </summary>
        public static void Scan(
            string directory,
            string ns,
            string pathPrefix,
            Action<string, ResourceId> onFile)
        {
            string[] jsonFiles = Directory.GetFiles(directory, "*.json");

            for (int i = 0; i < jsonFiles.Length; i++)
            {
                string filePath = jsonFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string idName = string.IsNullOrEmpty(pathPrefix)
                    ? fileName
                    : pathPrefix + "/" + fileName;
                ResourceId id = new ResourceId(ns, idName);
                onFile(filePath, id);
            }

            string[] subDirs = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirs.Length; i++)
            {
                string subDirName = Path.GetFileName(subDirs[i]);
                string newPrefix = string.IsNullOrEmpty(pathPrefix)
                    ? subDirName
                    : pathPrefix + "/" + subDirName;
                Scan(subDirs[i], ns, newPrefix, onFile);
            }
        }
    }
}
