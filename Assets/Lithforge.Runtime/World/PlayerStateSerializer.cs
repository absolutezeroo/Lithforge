using System;
using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Voxel.Storage;
using UnityEngine;

namespace Lithforge.Runtime.World
{
    /// <summary>
    /// Snapshots and restores player state (position, rotation, inventory, time of day)
    /// so sessions can survive save/load round-trips through <see cref="WorldMetadata"/>.
    /// </summary>
    public static class PlayerStateSerializer
    {
        /// <summary>
        /// Takes a snapshot of the player's current world state and packs it into
        /// a serializable <see cref="WorldPlayerState"/> for persistence.
        /// Only non-empty inventory slots are stored to keep save files compact.
        /// </summary>
        public static WorldPlayerState Capture(
            Transform playerTransform,
            Camera camera,
            float timeOfDay,
            Inventory inventory)
        {
            WorldPlayerState state = new();

            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                state.PosX = pos.x;
                state.PosY = pos.y;
                state.PosZ = pos.z;
#if LITHFORGE_DEBUG
                Debug.Log(
                    $"[PlayerStateSerializer] Capture: pos=({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
#endif
            }

            if (camera != null)
            {
                Vector3 euler = camera.transform.localEulerAngles;
                state.RotX = euler.x;

                if (playerTransform != null)
                {
                    state.RotY = playerTransform.eulerAngles.y;
                }
            }

            state.TimeOfDay = timeOfDay;

            if (inventory != null)
            {
                state.SelectedSlot = inventory.SelectedSlot;

                // Count non-empty slots
                int count = 0;

                for (int i = 0; i < Inventory.SlotCount; i++)
                {
                    ItemStack stack = inventory.GetSlot(i);

                    if (stack.Count > 0)
                    {
                        count++;
                    }
                }

                SavedItemStack[] slots = new SavedItemStack[count];
                int idx = 0;

                for (int i = 0; i < Inventory.SlotCount; i++)
                {
                    ItemStack stack = inventory.GetSlot(i);

                    if (stack.Count > 0)
                    {
                        List<SavedComponentEntry> components = null;

                        if (stack.HasComponents)
                        {
                            components = new List<SavedComponentEntry>();

                            foreach (KeyValuePair<int, IDataComponent> kvp in stack.Components)
                            {
                                using (MemoryStream ms = new())
                                {
                                    using (BinaryWriter w = new(ms))
                                    {
                                        DataComponentRegistry.SerializeComponent(kvp.Value, w);
                                    }

                                    string base64 = Convert.ToBase64String(ms.ToArray());
                                    components.Add(new SavedComponentEntry(kvp.Key, base64));
                                }
                            }
                        }

                        slots[idx] = new SavedItemStack(
                            i,
                            stack.ItemId.Namespace,
                            stack.ItemId.Name,
                            stack.Count,
                            stack.Durability,
                            components);
                        idx++;
                    }
                }

                state.Slots = slots;
            }

            return state;
        }

        /// <summary>
        /// Applies a previously captured state back onto the player, camera, and inventory.
        /// Items whose <see cref="ResourceId"/> no longer exists in the registry are silently
        /// dropped with a warning, so saves remain forward-compatible across content changes.
        /// </summary>
        public static void Restore(
            WorldPlayerState state,
            Transform playerTransform,
            Camera camera,
            Inventory inventory,
            ItemRegistry itemRegistry,
            out float restoredTimeOfDay)
        {
            restoredTimeOfDay = 0f;

            if (state == null)
            {
                return;
            }

            // Restore position
            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(state.PosX, state.PosY, state.PosZ);
                playerTransform.rotation = Quaternion.Euler(0f, state.RotY, 0f);
            }

            // Restore camera pitch
            if (camera != null)
            {
                Vector3 euler = camera.transform.localEulerAngles;
                euler.x = state.RotX;
                camera.transform.localEulerAngles = euler;
            }

            restoredTimeOfDay = (float)state.TimeOfDay;

            // Restore inventory
            if (inventory != null && state.Slots != null && itemRegistry != null)
            {
                inventory.Clear();

                for (int i = 0; i < state.Slots.Length; i++)
                {
                    SavedItemStack saved = state.Slots[i];

                    if (saved == null || saved.Count <= 0)
                    {
                        continue;
                    }

                    if (saved.Slot < 0 || saved.Slot >= Inventory.SlotCount)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[PlayerStateSerializer] Slot index {saved.Slot} out of range (0-{Inventory.SlotCount - 1}), skipping.");
                        continue;
                    }

                    ResourceId itemId = new(saved.Ns, saved.Name);

                    // Allow items with component data even if not in registry,
                    // since they carry all their data in the serialized components
                    if (!itemRegistry.Contains(itemId) && !saved.HasData)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[PlayerStateSerializer] Item '{saved.Ns}:{saved.Name}' not found in registry, slot {saved.Slot} cleared.");
                        continue;
                    }

                    ItemStack stack = saved.Durability >= 0
                        ? new ItemStack(itemId, saved.Count, saved.Durability)
                        : new ItemStack(itemId, saved.Count);

                    // New format: typed components
                    if (saved.Components != null && saved.Components.Count > 0)
                    {
                        DataComponentMap map = new();

                        for (int c = 0; c < saved.Components.Count; c++)
                        {
                            SavedComponentEntry entry = saved.Components[c];

                            if (entry == null || string.IsNullOrEmpty(entry.DataBase64))
                            {
                                continue;
                            }

                            byte[] data = Convert.FromBase64String(entry.DataBase64);

                            using (MemoryStream ms = new(data))
                            using (BinaryReader reader = new(ms))
                            {
                                IDataComponent component =
                                    DataComponentRegistry.DeserializeComponent(entry.TypeId, reader);

                                if (component != null)
                                {
                                    map.Set(entry.TypeId, component);
                                }
                            }
                        }

                        stack.Components = map.IsEmpty ? null : map;
                    }
                    // Legacy format: raw CustomData base64
                    else if (!string.IsNullOrEmpty(saved.CustomDataBase64))
                    {
                        byte[] customData = Convert.FromBase64String(saved.CustomDataBase64);
                        stack.Components = LegacyCustomDataMigrator.Migrate(customData);
                    }

                    inventory.SetSlot(saved.Slot, stack);
                }
            }
        }
    }
}
