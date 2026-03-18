using System;
using System.Collections.Generic;

using Lithforge.Core.Data;

namespace Lithforge.Item
{
    /// <summary>
    ///     Assembles a ToolInstance from a tool type and an array of ToolParts.
    ///     Resolves material stats from ToolMaterialRegistry and collects trait IDs.
    /// </summary>
    public static class ToolAssembler
    {
        /// <summary>
        ///     Assembles a ToolInstance from parts using material data from the registry.
        ///     Returns null if no valid head/blade part is found.
        /// </summary>
        public static ToolInstance Assemble(
            ToolType toolType,
            ToolPart[] parts,
            ToolMaterialRegistry materialRegistry)
        {
            if (parts == null || parts.Length == 0)
            {
                return null;
            }

            float baseSpeed = 1.0f;
            int baseDurability = 0;
            float baseDamage = 1.0f;
            int toolLevel = 0;
            float durabilityMultiplier = 1.0f;
            float speedMultiplier = 1.0f;
            bool hasHead = false;

            for (int i = 0; i < parts.Length; i++)
            {
                ToolPart part = parts[i];
                ToolMaterialData mat = materialRegistry.Get(part.MaterialId);

                if (mat == null)
                {
                    continue;
                }

                switch (part.PartType)
                {
                    case ToolPartType.Head:
                    case ToolPartType.Blade:
                    case ToolPartType.Point:
                        baseSpeed = mat.HeadMiningSpeed;
                        baseDurability = mat.HeadDurability;
                        baseDamage = mat.HeadAttackDamage;
                        toolLevel = mat.ToolLevel;
                        hasHead = true;

                        // Bake resolved stats into part
                        part.SpeedContribution = mat.HeadMiningSpeed;
                        part.DurabilityContribution = mat.HeadDurability;
                        part.DamageContribution = mat.HeadAttackDamage;
                        part.TraitIds = mat.TraitIds != null
                            ? ConvertTraitIds(mat.TraitIds) : Array.Empty<ResourceId>();
                        break;

                    case ToolPartType.Handle:
                    case ToolPartType.Shaft:
                    case ToolPartType.Grip:
                    case ToolPartType.Stock:
                        durabilityMultiplier = mat.HandleDurabilityMultiplier;
                        speedMultiplier = mat.HandleSpeedMultiplier;

                        part.DurabilityMultiplier = mat.HandleDurabilityMultiplier;
                        part.SpeedMultiplier = mat.HandleSpeedMultiplier;
                        part.TraitIds = mat.TraitIds != null
                            ? ConvertTraitIds(mat.TraitIds) : Array.Empty<ResourceId>();
                        break;

                    case ToolPartType.Binding:
                    case ToolPartType.Guard:
                        baseDurability += mat.BindingDurabilityBonus;

                        part.DurabilityContribution = mat.BindingDurabilityBonus;
                        part.TraitIds = mat.TraitIds != null
                            ? ConvertTraitIds(mat.TraitIds) : Array.Empty<ResourceId>();
                        break;
                }

                parts[i] = part;
            }

            if (!hasHead)
            {
                return null;
            }

            int maxDurability = (int)(baseDurability * durabilityMultiplier);

            if (maxDurability < 1)
            {
                maxDurability = 1;
            }

            ToolInstance tool = new()
            {
                ToolType = toolType,
                Parts = parts,
                Slots = new ModifierSlot[ToolInstance.MaxModifierSlots],
                CurrentDurability = maxDurability,
                MaxDurability = maxDurability,
                BaseSpeed = baseSpeed * speedMultiplier,
                BaseDamage = baseDamage,
                EffectiveToolLevel = toolLevel,
            };

            return tool;
        }

        private static ResourceId[] ConvertTraitIds(string[] traitIds)
        {
            List<ResourceId> result = new(traitIds.Length);

            for (int i = 0; i < traitIds.Length; i++)
            {
                if (ResourceId.TryParse(traitIds[i], out ResourceId rid))
                {
                    result.Add(rid);
                }
            }

            return result.ToArray();
        }
    }
}
