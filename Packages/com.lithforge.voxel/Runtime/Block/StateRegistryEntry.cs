using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Entry mapping a block to its contiguous StateId range.
    /// </summary>
    public sealed class StateRegistryEntry
    {
        /// <summary>Namespaced block identifier (e.g. "lithforge:stone").</summary>
        public ResourceId Id { get; }

        /// <summary>First StateId in this block's contiguous range.</summary>
        public ushort BaseStateId { get; }

        /// <summary>Number of states in this block's range.</summary>
        public int StateCount { get; }

        /// <summary>Sequential registration index of this block.</summary>
        public ushort BlockOrdinal { get; }

        /// <summary>Loot table ResourceId string for block drops.</summary>
        public string LootTable { get; }

        /// <summary>Mining hardness value. -1 = unbreakable.</summary>
        public float Hardness { get; }

        /// <summary>Explosion resistance value.</summary>
        public float BlastResistance { get; }

        /// <summary>Whether the correct tool is required to harvest.</summary>
        public bool RequiresTool { get; }

        /// <summary>Physical material category for tool speed modifiers.</summary>
        public BlockMaterialType MaterialType { get; }

        /// <summary>Minimum tool tier required to harvest.</summary>
        public int RequiredToolLevel { get; }

        /// <summary>Sound group name for break/place/step sounds.</summary>
        public string SoundGroup { get; }

        /// <summary>
        /// The block entity type identifier for this block, or null if none.
        /// Set via StateRegistry.PatchBlockEntityType() after registration.
        /// </summary>
        public string BlockEntityTypeId { get; internal set; }

        /// <summary>Creates a state registry entry with all block properties. Internal to StateRegistry.</summary>
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
