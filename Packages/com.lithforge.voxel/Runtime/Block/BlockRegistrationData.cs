using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Lightweight data struct for registering a block in StateRegistry.
    /// Contains only the fields needed for state computation and baking.
    /// Replaces direct BlockDefinition dependency in the registration path.
    /// </summary>
    public sealed class BlockRegistrationData
    {
        /// <summary>Namespaced block identifier (e.g. "lithforge:stone").</summary>
        public ResourceId Id { get; }

        /// <summary>Number of distinct block states for this block (cartesian product of properties).</summary>
        public int StateCount { get; }

        /// <summary>Render layer name ("opaque", "cutout", "translucent").</summary>
        public string RenderLayer { get; }

        /// <summary>Collision shape name ("full_cube", "none").</summary>
        public string CollisionShape { get; }

        /// <summary>Light emission level (0-15). 0 = does not emit light.</summary>
        public int LightEmission { get; }

        /// <summary>Light filter attenuation (0 = transparent, higher = more blocking).</summary>
        public int LightFilter { get; }

        /// <summary>Hex color string for minimap rendering.</summary>
        public string MapColor { get; }

        /// <summary>Loot table ResourceId string, or null for no drops.</summary>
        public string LootTable { get; }

        /// <summary>Mining hardness (time-to-break base value). -1 = unbreakable.</summary>
        public float Hardness { get; }

        /// <summary>Explosion resistance value.</summary>
        public float BlastResistance { get; }

        /// <summary>Whether the correct tool type is required to get drops.</summary>
        public bool RequiresTool { get; }

        /// <summary>Whether this block behaves as a fluid (water, lava).</summary>
        public bool IsFluid { get; }

        /// <summary>Physical material category for tool speed modifiers.</summary>
        public BlockMaterialType MaterialType { get; }

        /// <summary>Minimum tool tier required to harvest this block.</summary>
        public int RequiredToolLevel { get; }

        /// <summary>Whether this block has an associated block entity.</summary>
        public bool HasBlockEntity { get; }

        /// <summary>Block entity type identifier, or null if no block entity.</summary>
        public string BlockEntityTypeId { get; }

        /// <summary>Sound group name for break/place/step sounds (default "stone").</summary>
        public string SoundGroup { get; }

        /// <summary>Creates a block registration data instance with all block properties.</summary>
        public BlockRegistrationData(
            ResourceId id,
            int stateCount,
            string renderLayer,
            string collisionShape,
            int lightEmission,
            int lightFilter,
            string mapColor,
            string lootTable,
            float hardness,
            float blastResistance,
            bool requiresTool,
            bool isFluid = false,
            BlockMaterialType materialType = BlockMaterialType.None,
            int requiredToolLevel = 0,
            bool hasBlockEntity = false,
            string blockEntityTypeId = null,
            string soundGroup = "stone")
        {
            Id = id;
            StateCount = stateCount;
            RenderLayer = renderLayer ?? "opaque";
            CollisionShape = collisionShape ?? "full_cube";
            LightEmission = lightEmission;
            LightFilter = lightFilter;
            MapColor = mapColor;
            LootTable = lootTable;
            Hardness = hardness;
            BlastResistance = blastResistance;
            RequiresTool = requiresTool;
            IsFluid = isFluid;
            MaterialType = materialType;
            RequiredToolLevel = requiredToolLevel;
            HasBlockEntity = hasBlockEntity;
            BlockEntityTypeId = blockEntityTypeId;
            SoundGroup = soundGroup ?? "stone";
        }
    }
}
