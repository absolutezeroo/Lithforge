using System;
using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Item
{
    public sealed class ToolInstance
    {
        public const int MaxModifierSlots = 5;

        public ToolType ToolType;
        public ToolPart[] Parts;
        public ModifierSlot[] Slots;
        public int CurrentDurability;
        public int MaxDurability;

        public float BaseSpeed;
        public float BaseDamage;
        public int EffectiveToolLevel;

        public ToolDurabilityState DurabilityState
        {
            get
            {
                float pct = MaxDurability > 0 ? (float)CurrentDurability / MaxDurability : 0f;
                if (pct > 0.50f)
                {
                    return ToolDurabilityState.New;
                }

                if (pct > 0.20f)
                {
                    return ToolDurabilityState.Worn;
                }

                if (pct > 0.05f)
                {
                    return ToolDurabilityState.Damaged;
                }

                return ToolDurabilityState.Critical;
            }
        }

        /// <summary>
        /// Returns the effective speed for the given block material.
        /// Will be enriched by Traits once created.
        /// </summary>
        public float GetEffectiveSpeed(BlockMaterialType mat)
        {
            return BaseSpeed;
        }

        public int GetEffectiveToolLevel()
        {
            return EffectiveToolLevel;
        }

        /// <summary>
        /// Collects all IToolTrait from parts. Stub — will be implemented when
        /// ToolMaterialDefinitionSO and Trait C# classes exist.
        /// </summary>
        public IToolTrait[] GetAllTraits()
        {
            return Array.Empty<IToolTrait>();
        }

        public int GetUsedSlots()
        {
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsOccupied)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetAvailableSlots()
        {
            return Slots.Length - GetUsedSlots();
        }
    }
}
