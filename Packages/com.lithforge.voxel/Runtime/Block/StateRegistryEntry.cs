using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Entry mapping a block to its contiguous StateId range.
    /// </summary>
    public sealed class StateRegistryEntry
    {
        public ResourceId Id { get; }
        public ushort BaseStateId { get; }
        public int StateCount { get; }
        public ushort BlockOrdinal { get; }
        public string LootTable { get; }
        public float Hardness { get; }
        public float BlastResistance { get; }
        public bool RequiresTool { get; }

        internal StateRegistryEntry(
            ResourceId id,
            ushort baseStateId,
            int stateCount,
            ushort blockOrdinal,
            string lootTable,
            float hardness,
            float blastResistance,
            bool requiresTool)
        {
            Id = id;
            BaseStateId = baseStateId;
            StateCount = stateCount;
            BlockOrdinal = blockOrdinal;
            LootTable = lootTable;
            Hardness = hardness;
            BlastResistance = blastResistance;
            RequiresTool = requiresTool;
        }
    }
}
