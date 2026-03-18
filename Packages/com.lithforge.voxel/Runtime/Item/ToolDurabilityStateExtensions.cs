namespace Lithforge.Voxel.Item
{
    public static class ToolDurabilityStateExtensions
    {
        /// <summary>
        /// Returns a mining speed multiplier based on the tool's durability tier.
        /// Applied in <see cref="MiningContext.SpeedMultiplier"/> during mining calculations.
        /// </summary>
        public static float GetEffectiveSpeedMultiplier(this ToolDurabilityState state)
        {
            switch (state)
            {
                case ToolDurabilityState.New:
                    return 1.0f;
                case ToolDurabilityState.Worn:
                    return 1.0f;
                case ToolDurabilityState.Damaged:
                    return 0.75f;
                case ToolDurabilityState.Critical:
                    return 0.5f;
                case ToolDurabilityState.Broken:
                    return 0.0f;
                default:
                    return 1.0f;
            }
        }
    }
}
