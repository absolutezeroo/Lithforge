namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Entry mapping a BlockDefinition to its contiguous StateId range.
    /// </summary>
    public sealed class StateRegistryEntry
    {
        public BlockDefinition Definition { get; }
        public ushort BaseStateId { get; }
        public int StateCount { get; }
        public ushort BlockOrdinal { get; }

        internal StateRegistryEntry(BlockDefinition definition, ushort baseStateId, int stateCount, ushort blockOrdinal)
        {
            Definition = definition;
            BaseStateId = baseStateId;
            StateCount = stateCount;
            BlockOrdinal = blockOrdinal;
        }
    }
}
