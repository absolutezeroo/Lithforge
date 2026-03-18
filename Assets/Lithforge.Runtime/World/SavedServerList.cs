using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace Lithforge.Runtime.World
{
    /// <summary>
    ///     Persists the player's saved and recently connected servers to a JSON file
    ///     in <c>Application.persistentDataPath/servers.json</c>. Uses atomic writes
    ///     (write-to-temp, then rename) to prevent corruption on crash.
    /// </summary>
    public sealed class SavedServerList
    {
        private readonly string _filePath;
        private SavedServerListData _data;

        public SavedServerList()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "servers.json");
            Load();
        }

        /// <summary>Read-only view of the current server entries.</summary>
        public IReadOnlyList<SavedServerEntry> Entries
        {
            get { return _data.servers; }
        }

        /// <summary>
        ///     Adds or updates a server entry. If a server with the same address
        ///     and port exists, it is updated; otherwise a new entry is appended.
        ///     Auto-saves after modification.
        /// </summary>
        public void AddOrUpdate(SavedServerEntry entry)
        {
            int existingIndex = FindIndex(entry.address, entry.port);

            if (existingIndex >= 0)
            {
                _data.servers[existingIndex] = entry;
            }
            else
            {
                _data.servers.Add(entry);
            }

            Save();
        }

        /// <summary>
        ///     Removes a server by address and port. Auto-saves after modification.
        /// </summary>
        public void Remove(string address, ushort port)
        {
            int index = FindIndex(address, port);

            if (index >= 0)
            {
                _data.servers.RemoveAt(index);
                Save();
            }
        }

        /// <summary>
        ///     Finds the most recently connected server, or null if the list is empty.
        /// </summary>
        public SavedServerEntry GetMostRecent()
        {
            SavedServerEntry best = null;
            DateTime bestTime = DateTime.MinValue;

            for (int i = 0; i < _data.servers.Count; i++)
            {
                SavedServerEntry entry = _data.servers[i];

                if (!string.IsNullOrEmpty(entry.lastConnected) &&
                    DateTime.TryParse(entry.lastConnected, out DateTime parsed) &&
                    parsed > bestTime)
                {
                    bestTime = parsed;
                    best = entry;
                }
            }

            return best;
        }

        private int FindIndex(string address, ushort port)
        {
            for (int i = 0; i < _data.servers.Count; i++)
            {
                SavedServerEntry entry = _data.servers[i];

                if (string.Equals(entry.address, address, StringComparison.OrdinalIgnoreCase) &&
                    entry.port == port)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Load()
        {
            _data = new SavedServerListData();

            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                SavedServerListData loaded = JsonUtility.FromJson<SavedServerListData>(json);

                if (loaded != null && loaded.servers != null)
                {
                    _data = loaded;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SavedServerList] Failed to load {_filePath}: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                string tempPath = _filePath + ".tmp";
                string bakPath = _filePath + ".bak";

                File.WriteAllText(tempPath, json);

                // Atomic rotate: original → .bak, then .tmp → original
                if (File.Exists(_filePath))
                {
                    if (File.Exists(bakPath))
                    {
                        File.Delete(bakPath);
                    }

                    File.Move(_filePath, bakPath);
                }

                File.Move(tempPath, _filePath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SavedServerList] Failed to save: {ex.Message}");
            }
        }

        /// <summary>
        ///     Internal serialization container for JsonUtility.
        /// </summary>
        [Serializable]
        private sealed class SavedServerListData
        {
            public int version = 1;
            public List<SavedServerEntry> servers = new();
        }
    }
}
