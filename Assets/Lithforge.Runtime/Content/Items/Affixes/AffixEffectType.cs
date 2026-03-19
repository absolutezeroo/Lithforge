namespace Lithforge.Runtime.Content.Items.Affixes
{
    /// <summary>
    /// Types of effects an affix can apply to mining calculations.
    /// </summary>
    [System.Obsolete("Affix system has no assets and is unused. May be reactivated later.")]
    public enum AffixEffectType
    {
        /// <summary>Multiplies mining speed by a factor.</summary>
        SpeedMultiplier   = 0,

        /// <summary>Adds a flat bonus to mining speed.</summary>
        FlatSpeedBonus    = 1,

        /// <summary>Reduces the effective hardness of the target block.</summary>
        HardnessReduction = 2,

        /// <summary>Grants the ability to harvest blocks that normally require a specific tool.</summary>
        GrantHarvest      = 3,
    }
}
