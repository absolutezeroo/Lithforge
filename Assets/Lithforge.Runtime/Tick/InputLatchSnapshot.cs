namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Read-only snapshot of latched (edge-triggered) inputs for one tick.
    /// Produced by PlayerInputLatch.ConsumeTick().
    /// Value type — no heap allocation.
    /// </summary>
    public struct InputLatchSnapshot
    {
        public bool JumpPressed;
        public bool FlyTogglePressed;
        public bool NoclipTogglePressed;
        public bool RightClickPressed;

        /// <summary>0 = no digit key pressed; 1-9 = hotbar slot index (1-based).</summary>
        public int HotbarSlot;

        /// <summary>Net scroll clicks this tick (positive = up, negative = down).</summary>
        public int ScrollDelta;
    }
}
