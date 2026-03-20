using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Loads and persists server admin data: operators, bans, and whitelist.
    ///     Files are stored in worldDir/admin/ as ops.json, bans.json, whitelist.json.
    /// </summary>
    public sealed class AdminStore
    {
        /// <summary>Root directory for admin files (worldDir/admin/).</summary>
        private readonly string _adminDir;

        /// <summary>Set of banned player UUIDs for O(1) lookup.</summary>
        private readonly HashSet<string> _bannedUuids = new();

        /// <summary>Set of banned IP addresses for O(1) lookup.</summary>
        private readonly HashSet<string> _bannedIps = new();

        /// <summary>Full ban entries for serialization and expiry checking.</summary>
        private readonly List<BanEntry> _banEntries = new();

        /// <summary>Set of operator UUIDs for O(1) lookup.</summary>
        private readonly HashSet<string> _ops = new();

        /// <summary>Set of whitelisted UUIDs for O(1) lookup.</summary>
        private readonly HashSet<string> _whitelist = new();

        /// <summary>Whether whitelist enforcement is enabled.</summary>
        public bool WhitelistEnabled { get; set; }

        /// <summary>Creates an admin store rooted at the given world directory.</summary>
        public AdminStore(string worldDirectory)
        {
            _adminDir = Path.Combine(worldDirectory, "admin");
        }

        /// <summary>Loads all admin data from disk. Safe to call if files do not exist.</summary>
        public void Load()
        {
            if (!Directory.Exists(_adminDir))
            {
                return;
            }

            LoadOps();
            LoadBans();
            LoadWhitelist();
        }

        /// <summary>Returns true if the given UUID has operator privileges.</summary>
        public bool IsOp(string uuid)
        {
            return _ops.Contains(uuid);
        }

        /// <summary>
        ///     Returns true if the player is banned (by UUID or IP).
        ///     Expired bans are ignored.
        /// </summary>
        public bool IsBanned(string uuid, string ip)
        {
            for (int i = _banEntries.Count - 1; i >= 0; i--)
            {
                BanEntry entry = _banEntries[i];

                if (entry.IsExpired)
                {
                    _bannedUuids.Remove(entry.PlayerUuid);

                    if (!string.IsNullOrEmpty(entry.IpAddress))
                    {
                        _bannedIps.Remove(entry.IpAddress);
                    }

                    _banEntries.RemoveAt(i);
                    continue;
                }

                if (entry.PlayerUuid == uuid)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(ip) && entry.IpAddress == ip)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns true if the given UUID is on the whitelist.</summary>
        public bool IsWhitelisted(string uuid)
        {
            return _whitelist.Contains(uuid);
        }

        /// <summary>Grants operator status to the given UUID.</summary>
        public void AddOp(string uuid)
        {
            _ops.Add(uuid);
        }

        /// <summary>Revokes operator status from the given UUID.</summary>
        public void RemoveOp(string uuid)
        {
            _ops.Remove(uuid);
        }

        /// <summary>Adds a ban entry for the given player.</summary>
        public void Ban(BanEntry entry)
        {
            if (!_bannedUuids.Contains(entry.PlayerUuid))
            {
                _bannedUuids.Add(entry.PlayerUuid);
                _banEntries.Add(entry);
            }

            if (!string.IsNullOrEmpty(entry.IpAddress))
            {
                _bannedIps.Add(entry.IpAddress);
            }
        }

        /// <summary>Removes all ban entries for the given UUID.</summary>
        public void Unban(string uuid)
        {
            _bannedUuids.Remove(uuid);

            for (int i = _banEntries.Count - 1; i >= 0; i--)
            {
                if (_banEntries[i].PlayerUuid == uuid)
                {
                    if (!string.IsNullOrEmpty(_banEntries[i].IpAddress))
                    {
                        _bannedIps.Remove(_banEntries[i].IpAddress);
                    }

                    _banEntries.RemoveAt(i);
                }
            }
        }

        /// <summary>Adds a UUID to the whitelist.</summary>
        public void AddWhitelist(string uuid)
        {
            _whitelist.Add(uuid);
        }

        /// <summary>Removes a UUID from the whitelist.</summary>
        public void RemoveWhitelist(string uuid)
        {
            _whitelist.Remove(uuid);
        }

        /// <summary>Persists all admin data (ops, bans, whitelist) to disk.</summary>
        public void Save()
        {
            if (!Directory.Exists(_adminDir))
            {
                Directory.CreateDirectory(_adminDir);
            }

            SaveOps();
            SaveBans();
            SaveWhitelist();
        }

        /// <summary>Loads operator UUIDs from ops.json.</summary>
        private void LoadOps()
        {
            string path = Path.Combine(_adminDir, "ops.json");

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JArray arr = JArray.Parse(json);

                for (int i = 0; i < arr.Count; i++)
                {
                    string uuid = arr[i]?.Value<string>();

                    if (!string.IsNullOrEmpty(uuid))
                    {
                        _ops.Add(uuid);
                    }
                }
            }
            catch
            {
                // Corrupted file — start with empty ops
            }
        }

        /// <summary>Loads ban entries from bans.json.</summary>
        private void LoadBans()
        {
            string path = Path.Combine(_adminDir, "bans.json");

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JArray arr = JArray.Parse(json);

                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is not JObject obj)
                    {
                        continue;
                    }

                    BanEntry entry = new()
                    {
                        PlayerUuid = obj["uuid"]?.Value<string>() ?? "",
                        IpAddress = obj["ip"]?.Value<string>() ?? "",
                        Reason = obj["reason"]?.Value<string>() ?? "",
                        BannedBy = obj["banned_by"]?.Value<string>() ?? "",
                    };

                    string createdStr = obj["created_at"]?.Value<string>();

                    if (!string.IsNullOrEmpty(createdStr)
                        && DateTime.TryParse(createdStr, out DateTime created))
                    {
                        entry.CreatedAt = created;
                    }

                    string expiresStr = obj["expires_at"]?.Value<string>();

                    if (!string.IsNullOrEmpty(expiresStr)
                        && DateTime.TryParse(expiresStr, out DateTime expires))
                    {
                        entry.ExpiresAt = expires;
                    }

                    if (!entry.IsExpired)
                    {
                        _bannedUuids.Add(entry.PlayerUuid);

                        if (!string.IsNullOrEmpty(entry.IpAddress))
                        {
                            _bannedIps.Add(entry.IpAddress);
                        }

                        _banEntries.Add(entry);
                    }
                }
            }
            catch
            {
                // Corrupted file — start with empty bans
            }
        }

        /// <summary>Loads whitelist UUIDs from whitelist.json.</summary>
        private void LoadWhitelist()
        {
            string path = Path.Combine(_adminDir, "whitelist.json");

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JArray arr = JArray.Parse(json);

                for (int i = 0; i < arr.Count; i++)
                {
                    string uuid = arr[i]?.Value<string>();

                    if (!string.IsNullOrEmpty(uuid))
                    {
                        _whitelist.Add(uuid);
                    }
                }
            }
            catch
            {
                // Corrupted file — start with empty whitelist
            }
        }

        /// <summary>Saves operator UUIDs to ops.json.</summary>
        private void SaveOps()
        {
            JArray arr = new();

            foreach (string uuid in _ops)
            {
                arr.Add(uuid);
            }

            string path = Path.Combine(_adminDir, "ops.json");
            File.WriteAllText(path, arr.ToString(Formatting.Indented));
        }

        /// <summary>Saves ban entries to bans.json.</summary>
        private void SaveBans()
        {
            JArray arr = new();

            for (int i = 0; i < _banEntries.Count; i++)
            {
                BanEntry entry = _banEntries[i];

                if (entry.IsExpired)
                {
                    continue;
                }

                JObject obj = new()
                {
                    ["uuid"] = entry.PlayerUuid,
                    ["ip"] = entry.IpAddress,
                    ["reason"] = entry.Reason,
                    ["banned_by"] = entry.BannedBy,
                    ["created_at"] = entry.CreatedAt.ToString("o"),
                };

                if (entry.ExpiresAt.HasValue)
                {
                    obj["expires_at"] = entry.ExpiresAt.Value.ToString("o");
                }

                arr.Add(obj);
            }

            string path = Path.Combine(_adminDir, "bans.json");
            File.WriteAllText(path, arr.ToString(Formatting.Indented));
        }

        /// <summary>Saves whitelist UUIDs to whitelist.json.</summary>
        private void SaveWhitelist()
        {
            JArray arr = new();

            foreach (string uuid in _whitelist)
            {
                arr.Add(uuid);
            }

            string path = Path.Combine(_adminDir, "whitelist.json");
            File.WriteAllText(path, arr.ToString(Formatting.Indented));
        }
    }
}
