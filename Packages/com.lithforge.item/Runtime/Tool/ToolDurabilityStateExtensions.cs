namespace Lithforge.Item
{
    public static class ToolDurabilityStateExtensions
    {
        /// <summary>
        /// Returns a mining speed multiplier based on the tool's durability tier.
        /// Applied in <see cref="MiningContext.SpeedMultiplier"/> during mining calculations.
        /// </summary>
        public static float GetEffectiveSpeedMultiplier(this ToolDurabilityState state)
        {
            return state switch
            {
                ToolDurabilityState.New => 1.0f,
                ToolDurabilityState.Worn => 1.0f,
                ToolDurabilityState.Damaged => 0.75f,
                ToolDurabilityState.Critical => 0.5f,
                ToolDurabilityState.Broken => 0.0f,
                _ => 1.0f,
            };
        }
    }
}
