using System;
using Lithforge.Core.Data;
using Lithforge.Voxel.Item;
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
        /// <param name="playerTransform">Player root transform for position and yaw. May be null.</param>
        /// <param name="camera">Main camera for pitch angle. May be null.</param>
        /// <param name="timeOfDay">Current day-cycle time (0..1) to restore on reload.</param>
        /// <param name="inventory">Player inventory. May be null if no items need saving.</param>
        /// <returns>A fully populated state object ready for JSON serialization.</returns>
        public static WorldPlayerState Capture(
            Transform playerTransform,
            Camera camera,
            float timeOfDay,
            Inventory inventory)
        {
            WorldPlayerState state = new WorldPlayerState();

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
                        string customBase64 = stack.HasCustomData
                            ? Convert.ToBase64String(stack.CustomData)
                            : null;

                        slots[idx] = new SavedItemStack(
                            i,
                            stack.ItemId.Namespace,
                            stack.ItemId.Name,
                            stack.Count,
                            stack.Durability,
                            customBase64);
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
        /// <param name="state">Saved state to restore. If null, the method is a no-op.</param>
        /// <param name="playerTransform">Player root transform to reposition. May be null.</param>
        /// <param name="camera">Main camera whose pitch will be restored. May be null.</param>
        /// <param name="inventory">Inventory to clear and repopulate. May be null.</param>
        /// <param name="itemRegistry">Used to validate that saved item IDs still exist.</param>
        /// <param name="restoredTimeOfDay">Receives the saved time-of-day value, or 0 if state is null.</param>
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

                    ResourceId itemId = new ResourceId(saved.Ns, saved.Name);

                    if (!itemRegistry.Contains(itemId))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[PlayerStateSerializer] Item '{saved.Ns}:{saved.Name}' not found in registry, slot {saved.Slot} cleared.");
                        continue;
                    }

                    ItemStack stack = saved.Durability >= 0
                        ? new ItemStack(itemId, saved.Count, saved.Durability)
                        : new ItemStack(itemId, saved.Count);

                    if (!string.IsNullOrEmpty(saved.CustomDataBase64))
                    {
                        stack.CustomData = Convert.FromBase64String(saved.CustomDataBase64);
                    }

                    inventory.SetSlot(saved.Slot, stack);
                }
            }
        }
    }
}
