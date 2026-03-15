using Lithforge.Core.Data;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Storage;
using UnityEngine;

namespace Lithforge.Runtime.World
{
    public static class PlayerStateSerializer
    {
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
                        slots[idx] = new SavedItemStack(
                            i,
                            stack.ItemId.Namespace,
                            stack.ItemId.Name,
                            stack.Count,
                            stack.Durability);
                        idx++;
                    }
                }

                state.Slots = slots;
            }

            return state;
        }

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

                    inventory.SetSlot(saved.Slot, stack);
                }
            }
        }
    }
}
