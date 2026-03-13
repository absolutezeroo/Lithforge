using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.Content.Items
{
    /// <summary>
    /// A single mining effect applied by an affix or enchantment level.
    /// Conditionally modifies a MiningContext based on material and tool type filters.
    /// </summary>
    [System.Serializable]
    public struct AffixMiningEffect
    {
        public AffixEffectType Type;
        public float Value;
        public BlockMaterialType TargetMaterial;
        public ToolType TargetToolType;

        public MiningContext Apply(MiningContext ctx)
        {
            bool matMatch = TargetMaterial == BlockMaterialType.None
                || TargetMaterial == ctx.Material;
            bool toolMatch = TargetToolType == ToolType.None
                || TargetToolType == ctx.ToolType;

            if (!matMatch || !toolMatch)
            {
                return ctx;
            }

            switch (Type)
            {
                case AffixEffectType.SpeedMultiplier:
                    ctx.SpeedMultiplier *= Value;
                    break;
                case AffixEffectType.FlatSpeedBonus:
                    ctx.FlatSpeedBonus += Value;
                    break;
                case AffixEffectType.HardnessReduction:
                    ctx.HardnessReduction += Value;
                    break;
                case AffixEffectType.GrantHarvest:
                    ctx.CanHarvest = true;
                    break;
            }

            return ctx;
        }
    }
}
