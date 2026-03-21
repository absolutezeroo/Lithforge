using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Reads and writes per-player save data to playerdata/&lt;uuid&gt;.json files.
    ///     Uses the same atomic temp+rotate+rename write pattern as <see cref="WorldMetadata" />.
    /// </summary>
    public sealed class PlayerDataStore
    {
        /// <summary>Root directory for player data files (worldDir/playerdata/).</summary>
        private readonly string _playerDataDir;

        /// <summary>Creates a player data store rooted at the given world directory.</summary>
        public PlayerDataStore(string worldDirectory)
        {
            _playerDataDir = Path.Combine(worldDirectory, "playerdata");
        }

        /// <summary>Returns true if a save file exists for the given player UUID.</summary>
        public bool Exists(string uuid)
        {
            string safeName = Path.GetFileName(uuid);
            string filePath = Path.Combine(_playerDataDir, safeName + ".json");

            return File.Exists(filePath);
        }

        /// <summary>
        ///     Loads a player's saved state. Returns null if the file does not exist
        ///     or if parsing fails.
        /// </summary>
        public WorldPlayerState Load(string uuid)
        {
            string safeName = Path.GetFileName(uuid);
            string filePath = Path.Combine(_playerDataDir, safeName + ".json");

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                JObject root = JObject.Parse(json);

                JToken stateToken = root["state"];

                if (stateToken is not { Type: JTokenType.Object })
                {
                    return null;
                }

                JObject s = (JObject)stateToken;

                return DeserializeState(s);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Persists a player's state to disk using atomic write (temp + rename).
        ///     Creates the playerdata directory if it does not exist.
        /// </summary>
        public void Save(string uuid, WorldPlayerState state)
        {
            string safeName = Path.GetFileName(uuid);

            if (!Directory.Exists(_playerDataDir))
            {
                Directory.CreateDirectory(_playerDataDir);
            }

            string filePath = Path.Combine(_playerDataDir, safeName + ".json");

            JObject root = new()
            {
                ["uuid"] = uuid, ["format_version"] = PlayerDataFile.CurrentFormatVersion, ["state"] = SerializeState(state),
            };

            AtomicWrite(filePath, root.ToString(Formatting.Indented));
        }

        /// <summary>Serializes a WorldPlayerState to a JObject for JSON output.</summary>
        private static JObject SerializeState(WorldPlayerState state)
        {
            if (state is null)
            {
                return new JObject();
            }

            JObject s = new()
            {
                ["pos_x"] = state.PosX,
                ["pos_y"] = state.PosY,
                ["pos_z"] = state.PosZ,
                ["rot_x"] = state.RotX,
                ["rot_y"] = state.RotY,
                ["time_of_day"] = state.TimeOfDay,
                ["selected_slot"] = state.SelectedSlot,
            };

            if (state.Slots is { Length: > 0 })
            {
                JArray slots = new();

                for (int i = 0; i < state.Slots.Length; i++)
                {
                    SavedItemStack stack = state.Slots[i];

                    if (stack is { Count: > 0 })
                    {
                        JObject slot = new()
                        {
                            ["slot"] = stack.Slot,
                            ["ns"] = stack.Ns,
                            ["name"] = stack.Name,
                            ["count"] = stack.Count,
                            ["durability"] = stack.Durability,
                        };

                        if (stack.Components is { Count: > 0 })
                        {
                            JArray comps = new();

                            for (int c = 0; c < stack.Components.Count; c++)
                            {
                                SavedComponentEntry entry = stack.Components[c];
                                JObject comp = new()
                                {
                                    ["type"] = entry.TypeId, ["data"] = entry.DataBase64,
                                };
                                comps.Add(comp);
                            }

                            slot["components"] = comps;
                        }

                        slots.Add(slot);
                    }
                }

                s["slots"] = slots;
            }

            return s;
        }

        /// <summary>Deserializes a WorldPlayerState from a JObject.</summary>
        private static WorldPlayerState DeserializeState(JObject s)
        {
            WorldPlayerState state = new()
            {
                PosX = s["pos_x"]?.Value<float>() ?? 0f,
                PosY = s["pos_y"]?.Value<float>() ?? 0f,
                PosZ = s["pos_z"]?.Value<float>() ?? 0f,
                RotX = s["rot_x"]?.Value<float>() ?? 0f,
                RotY = s["rot_y"]?.Value<float>() ?? 0f,
                TimeOfDay = s["time_of_day"]?.Value<double>() ?? 0.0,
                SelectedSlot = s["selected_slot"]?.Value<int>() ?? 0,
            };

            if (s["slots"] is JArray { Count: > 0 } slotsArray)
            {
                SavedItemStack[] slots = new SavedItemStack[slotsArray.Count];

                for (int i = 0; i < slotsArray.Count; i++)
                {
                    if (slotsArray[i] is JObject slotObj)
                    {
                        SavedItemStack saved = new()
                        {
                            Slot = slotObj["slot"]?.Value<int>() ?? 0,
                            Ns = slotObj["ns"]?.Value<string>() ?? "",
                            Name = slotObj["name"]?.Value<string>() ?? "",
                            Count = slotObj["count"]?.Value<int>() ?? 0,
                            Durability = slotObj["durability"]?.Value<int>() ?? -1,
                        };

                        if (slotObj["components"] is JArray { Count: > 0 } compsArray)
                        {
                            List<SavedComponentEntry> components = new(compsArray.Count);

                            for (int c = 0; c < compsArray.Count; c++)
                            {
                                if (compsArray[c] is JObject compObj)
                                {
                                    int typeId = compObj["type"]?.Value<int>() ?? 0;
                                    string data = compObj["data"]?.Value<string>();
                                    components.Add(new SavedComponentEntry(typeId, data));
                                }
                            }

                            saved.Components = components;
                        }

                        slots[i] = saved;
                    }
                }

                state.Slots = slots;
            }

            return state;
        }

        /// <summary>Writes content to a file atomically via temp+backup+rename pattern.</summary>
        private static void AtomicWrite(string filePath, string content)
        {
            string tempPath = filePath + ".tmp";

            try
            {
                File.WriteAllText(tempPath, content);

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
    }
}
