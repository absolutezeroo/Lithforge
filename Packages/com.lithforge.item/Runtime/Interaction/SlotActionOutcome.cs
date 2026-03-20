namespace Lithforge.Item.Interaction
{
    /// <summary>Outcome codes for slot action execution.</summary>
    public enum SlotActionOutcome : byte
    {
        /// <summary>Action completed successfully.</summary>
        Success = 0,

        /// <summary>Slot index was out of valid range.</summary>
        InvalidSlot = 1,

        /// <summary>Click type was not recognized.</summary>
        InvalidAction = 2,

        /// <summary>Total item count changed during execution (anti-dupe violation).</summary>
        ItemCountMismatch = 3,
    }
}
