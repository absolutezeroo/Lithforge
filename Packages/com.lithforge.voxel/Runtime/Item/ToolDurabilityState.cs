namespace Lithforge.Voxel.Item
{
    public enum ToolDurabilityState : byte
    {
        New      = 0,
        Worn     = 1,
        Damaged  = 2,
        Critical = 3,
    }

    public static class ToolDurabilityStateExtensions
    {
        public static float GetEffectiveSpeedMultiplier(this ToolDurabilityState s)
        {
            return s switch
            {
                ToolDurabilityState.New      => 1.0f,
                ToolDurabilityState.Worn     => 0.9f,
                ToolDurabilityState.Damaged  => 0.7f,
                ToolDurabilityState.Critical => 0.4f,
                _                            => 1.0f,
            };
        }

        public static int GetCrackStage(this ToolDurabilityState s, float normalizedDur)
        {
            return s switch
            {
                ToolDurabilityState.New      => 0,
                ToolDurabilityState.Worn     => 1 + (int)((1f - normalizedDur / 0.5f) * 3),
                ToolDurabilityState.Damaged  => 4 + (int)((1f - normalizedDur / 0.2f) * 3),
                ToolDurabilityState.Critical => 7 + (int)((1f - normalizedDur / 0.05f) * 2),
                _                            => 0,
            };
        }
    }
}
