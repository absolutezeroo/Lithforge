using Lithforge.Voxel.Block;

namespace Lithforge.Item
{
    /// <summary>
    /// A single mining effect applied by a tool trait.
    /// Pure C# struct — Tier 2 equivalent of AffixMiningEffect.
    /// Conditionally modifies a MiningContext based on material and tool type filters.
    /// </summary>
    public struct ToolTraitEffect
    {
        public MiningEffectType Type;
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
                case MiningEffectType.SpeedMultiplier:
                    ctx.SpeedMultiplier *= Value;
                    break;
                case MiningEffectType.FlatSpeedBonus:
                    ctx.FlatSpeedBonus += Value;
                    break;
                case MiningEffectType.HardnessReduction:
                    ctx.HardnessReduction += Value;
                    break;
                case MiningEffectType.GrantHarvest:
                    ctx.CanHarvest = true;
                    break;
            }

            return ctx;
        }
    }
}
