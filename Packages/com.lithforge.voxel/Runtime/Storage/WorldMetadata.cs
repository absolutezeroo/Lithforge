using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lithforge.Voxel.Storage
{
    public sealed class WorldMetadata
    {
        public string DisplayName { get; set; } = "New World";
        public long Seed { get; set; }
        public GameMode GameMode { get; set; } = GameMode.Survival;
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime LastPlayed { get; set; } = DateTime.UtcNow;
        public int DataVersion { get; set; } = 2;
        public string ContentHash { get; set; } = "";
        public WorldPlayerState PlayerState { get; set; }

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

            if (PlayerState != null)
            {
                JObject player = new()
                {
                    ["pos_x"] = PlayerState.PosX,
                    ["pos_y"] = PlayerState.PosY,
                    ["pos_z"] = PlayerState.PosZ,
                    ["rot_x"] = PlayerState.RotX,
                    ["rot_y"] = PlayerState.RotY,
                    ["time_of_day"] = PlayerState.TimeOfDay,
                    ["selected_slot"] = PlayerState.SelectedSlot,
                };

                if (PlayerState.Slots is
                    {
                        Length: > 0,
                    })
                {
                    JArray slots = new();

                    for (int i = 0; i < PlayerState.Slots.Length; i++)
                    {
                        SavedItemStack stack = PlayerState.Slots[i];

                        if (stack is
                            {
                                Count: > 0,
                            })
                        {
                            JObject slot = new()
                            {
                                ["slot"] = stack.Slot,
                                ["ns"] = stack.Ns,
                                ["name"] = stack.Name,
                                ["count"] = stack.Count,
                                ["durability"] = stack.Durability,
                            };

                            // New format: components array
                            if (stack.Components is
                                {
                                    Count: > 0,
                                })
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

                    player["slots"] = slots;
                }

                root["player"] = player;
            }

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

                // Load player state if present
                JToken playerToken = root["player"];

                if (playerToken is
                    {
                        Type: JTokenType.Object,
                    })
                {
                    JObject playerObj = (JObject)playerToken;

                    WorldPlayerState playerState = new()
                    {
                        PosX = playerObj["pos_x"]?.Value<float>() ?? 0f,
                        PosY = playerObj["pos_y"]?.Value<float>() ?? 0f,
                        PosZ = playerObj["pos_z"]?.Value<float>() ?? 0f,
                        RotX = playerObj["rot_x"]?.Value<float>() ?? 0f,
                        RotY = playerObj["rot_y"]?.Value<float>() ?? 0f,
                        TimeOfDay = playerObj["time_of_day"]?.Value<double>() ?? 0.0,
                        SelectedSlot = playerObj["selected_slot"]?.Value<int>() ?? 0,
                    };

                    JArray slotsArray = playerObj["slots"] as JArray;

                    if (slotsArray is
                        {
                            Count: > 0,
                        })
                    {
                        SavedItemStack[] slots = new SavedItemStack[slotsArray.Count];

                        for (int i = 0; i < slotsArray.Count; i++)
                        {
                            JObject slotObj = slotsArray[i] as JObject;

                            if (slotObj != null)
                            {
                                SavedItemStack saved = new()
                                {
                                    Slot = slotObj["slot"]?.Value<int>() ?? 0,
                                    Ns = slotObj["ns"]?.Value<string>() ?? "",
                                    Name = slotObj["name"]?.Value<string>() ?? "",
                                    Count = slotObj["count"]?.Value<int>() ?? 0,
                                    Durability = slotObj["durability"]?.Value<int>() ?? -1,
                                };

                                // New format: components array
                                JArray compsArray = slotObj["components"] as JArray;

                                if (compsArray is
                                    {
                                        Count: > 0,
                                    })
                                {
                                    List<SavedComponentEntry> components = new(compsArray.Count);

                                    for (int c = 0; c < compsArray.Count; c++)
                                    {
                                        JObject compObj = compsArray[c] as JObject;

                                        if (compObj != null)
                                        {
                                            int typeId = compObj["type"]?.Value<int>() ?? 0;
                                            string data = compObj["data"]?.Value<string>();
                                            components.Add(new SavedComponentEntry(typeId, data));
                                        }
                                    }

                                    saved.Components = components;
                                }
                                else
                                {
                                    // Legacy format: custom_data string
                                    string customData = slotObj["custom_data"]?.Value<string>();

                                    if (!string.IsNullOrEmpty(customData))
                                    {
                                        saved.CustomDataBase64 = customData;
                                    }
                                }

                                slots[i] = saved;
                            }
                        }

                        playerState.Slots = slots;
                    }

                    meta.PlayerState = playerState;
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
