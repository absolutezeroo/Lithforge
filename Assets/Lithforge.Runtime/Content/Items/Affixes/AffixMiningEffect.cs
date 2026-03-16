using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;

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
        [FormerlySerializedAs("Type")]
        public AffixEffectType type;
        [FormerlySerializedAs("Value")]
        public float value;
        [FormerlySerializedAs("TargetMaterial")]
        public BlockMaterialType targetMaterial;
        [FormerlySerializedAs("TargetToolType")]
        public ToolType targetToolType;

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
