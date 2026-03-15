namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Types of effects a tool trait can apply to mining calculations.
    /// Tier 2 equivalent of AffixEffectType (Tier 3).
    /// </summary>
    public enum MiningEffectType : byte
    {
        SpeedMultiplier   = 0,
        FlatSpeedBonus    = 1,
        HardnessReduction = 2,
        GrantHarvest      = 3,
    }
}
