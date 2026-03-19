using Lithforge.Voxel.Block;
using Lithforge.Item;

using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items.Affixes
{
    /// <summary>
    /// A single mining effect applied by an affix or enchantment level.
    /// Conditionally modifies a MiningContext based on material and tool type filters.
    /// </summary>
    [System.Obsolete("Affix system has no assets and is unused. May be reactivated later.")]
    [System.Serializable]
    public struct AffixMiningEffect
    {
        /// <summary>Type of mining modification this effect applies.</summary>
        [FormerlySerializedAs("Type")]
        public AffixEffectType type;

        /// <summary>Magnitude of the effect (multiplier, flat bonus, or reduction amount).</summary>
        [FormerlySerializedAs("Value")]
        public float value;

        /// <summary>Block material this effect targets. None matches all materials.</summary>
        [FormerlySerializedAs("TargetMaterial")]
        public BlockMaterialType targetMaterial;

        /// <summary>Tool type this effect targets. None matches all tool types.</summary>
        [FormerlySerializedAs("TargetToolType")]
        public ToolType targetToolType;

        /// <summary>Applies this effect to the mining context if material and tool type match.</summary>
        public MiningContext Apply(MiningContext ctx)
        {
            bool matMatch = targetMaterial == BlockMaterialType.None
                || targetMaterial == ctx.Material;
            bool toolMatch = targetToolType == ToolType.None
                || targetToolType == ctx.ToolType;

            if (!matMatch || !toolMatch)
            {
                return ctx;
            }

            switch (type)
            {
                case AffixEffectType.SpeedMultiplier:
                    ctx.SpeedMultiplier *= value;
                    break;
                case AffixEffectType.FlatSpeedBonus:
                    ctx.FlatSpeedBonus += value;
                    break;
                case AffixEffectType.HardnessReduction:
                    ctx.HardnessReduction += value;
                    break;
                case AffixEffectType.GrantHarvest:
                    ctx.CanHarvest = true;
                    break;
            }

            return ctx;
        }
    }
}
