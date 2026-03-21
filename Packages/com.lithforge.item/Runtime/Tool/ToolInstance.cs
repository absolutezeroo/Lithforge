using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Item
{
    public sealed class ToolInstance
    {
        public const int MaxModifierSlots = 5;
        public float BaseDamage;

        public float BaseSpeed;
        public int CurrentDurability;
        public int EffectiveToolLevel;
        public bool IsBroken;
        public int MaxDurability;
        public ToolPart[] Parts;
        public ModifierSlot[] Slots;

        public ToolType ToolType;

        public ToolDurabilityState DurabilityState
        {
            get
            {
                if (IsBroken)
                {
                    return ToolDurabilityState.Broken;
                }

                float pct = MaxDurability > 0 ? (float)CurrentDurability / MaxDurability : 0f;

                switch (pct)
                {
                    case > 0.50f:
                        return ToolDurabilityState.New;
                    case > 0.20f:
                        return ToolDurabilityState.Worn;
                    case > 0.05f:
                        return ToolDurabilityState.Damaged;
                    default:
                        return ToolDurabilityState.Critical;
                }
            }
        }

        /// <summary>
        ///     Sets current durability with TiC setDamage semantics:
        ///     durability &lt;= 0 marks the tool as broken; durability &gt; 0 unbreaks it.
        /// </summary>
        public void SetCurrentDurability(int durability)
        {
            if (durability <= 0)
            {
                CurrentDurability = 0;
                IsBroken = true;
            }
            else
            {
                CurrentDurability = Math.Min(durability, MaxDurability);
                IsBroken = false;
            }
        }

        /// <summary>
        ///     Returns the effective mining speed. BaseSpeed is already computed
        ///     from head material + handle multiplier at assembly time.
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
        ///     Collects all IToolTrait from all parts by resolving TraitIds
        ///     through the trait registry. Deduplicates by TraitId, keeping
        ///     only the highest TraitLevel for each.
        /// </summary>
        public IToolTrait[] GetAllTraits(ToolTraitRegistry traitRegistry)
        {
            if (traitRegistry == null || Parts == null)
            {
                return Array.Empty<IToolTrait>();
            }

            Dictionary<string, ToolTraitData> best = new();

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
        ///     Parameterless overload for backward compatibility.
        ///     Returns empty when no registry is available.
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

        /// <summary>
        ///     Creates a deep copy of this ToolInstance.
        ///     All arrays (Parts, Slots, TraitIds) are cloned.
        /// </summary>
        public ToolInstance Clone()
        {
            ToolInstance copy = new()
            {
                ToolType = ToolType,
                BaseDamage = BaseDamage,
                BaseSpeed = BaseSpeed,
                CurrentDurability = CurrentDurability,
                MaxDurability = MaxDurability,
                IsBroken = IsBroken,
                EffectiveToolLevel = EffectiveToolLevel,
            };

            if (Parts != null)
            {
                copy.Parts = new ToolPart[Parts.Length];

                for (int i = 0; i < Parts.Length; i++)
                {
                    copy.Parts[i] = Parts[i];

                    if (Parts[i].TraitIds != null)
                    {
                        copy.Parts[i].TraitIds = new ResourceId[Parts[i].TraitIds.Length];
                        Array.Copy(Parts[i].TraitIds, copy.Parts[i].TraitIds, Parts[i].TraitIds.Length);
                    }
                }
            }

            if (Slots != null)
            {
                copy.Slots = new ModifierSlot[Slots.Length];
                Array.Copy(Slots, copy.Slots, Slots.Length);
            }

            return copy;
        }
    }
}
