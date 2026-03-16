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
        public BlockMaterialType MaterialType { get; }
        public int RequiredToolLevel { get; }
        public string SoundGroup { get; }

        /// <summary>
        /// The block entity type identifier for this block, or null if none.
        /// Set via StateRegistry.PatchBlockEntityType() after registration.
        /// </summary>
        public string BlockEntityTypeId { get; internal set; }

        internal StateRegistryEntry(
            ResourceId id,
            ushort baseStateId,
            int stateCount,
            ushort blockOrdinal,
            string lootTable,
            float hardness,
            float blastResistance,
            bool requiresTool,
            BlockMaterialType materialType,
            int requiredToolLevel,
            string soundGroup)
        {
            Id = id;
            BaseStateId = baseStateId;
            StateCount = stateCount;
            BlockOrdinal = blockOrdinal;
            LootTable = lootTable;
            Hardness = hardness;
            BlastResistance = blastResistance;
            RequiresTool = requiresTool;
            MaterialType = materialType;
            RequiredToolLevel = requiredToolLevel;
            SoundGroup = soundGroup ?? "stone";
        }
    }
}
