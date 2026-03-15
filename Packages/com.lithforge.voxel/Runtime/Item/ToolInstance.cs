using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
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
        /// Returns the effective mining speed. BaseSpeed is already computed
        /// from head material + handle multiplier at assembly time.
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
        /// Collects all IToolTrait from all parts by resolving TraitIds
        /// through the trait registry. Deduplicates by TraitId, keeping
        /// only the highest TraitLevel for each.
        /// </summary>
        public IToolTrait[] GetAllTraits(ToolTraitRegistry traitRegistry)
        {
            if (traitRegistry == null || Parts == null)
            {
                return Array.Empty<IToolTrait>();
            }

            Dictionary<string, ToolTraitData> best = new Dictionary<string, ToolTraitData>();

            for (int p = 0; p < Parts.Length; p++)
            {
                ResourceId[] traitIds = Parts[p].TraitIds;

                if (traitIds == null)
                {
                    continue;
                }

                for (int t = 0; t < traitIds.Length; t++)
                {
                    ToolTraitData trait = traitRegistry.Get(traitIds[t].ToString());

                    if (trait == null)
                    {
                        continue;
                    }

                    if (!best.TryGetValue(trait.TraitId, out ToolTraitData existing)
                        || trait.TraitLevel > existing.TraitLevel)
                    {
                        best[trait.TraitId] = trait;
                    }
                }
            }

            if (best.Count == 0)
            {
                return Array.Empty<IToolTrait>();
            }

            IToolTrait[] result = new IToolTrait[best.Count];
            int idx = 0;

            foreach (KeyValuePair<string, ToolTraitData> kvp in best)
            {
                result[idx++] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// Parameterless overload for backward compatibility.
        /// Returns empty when no registry is available.
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
