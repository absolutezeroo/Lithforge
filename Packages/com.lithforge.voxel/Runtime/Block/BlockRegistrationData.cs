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
        public ResourceId Id { get; }

        public int StateCount { get; }

        public string RenderLayer { get; }

        public string CollisionShape { get; }

        public int LightEmission { get; }

        public int LightFilter { get; }

        public string MapColor { get; }

        public string LootTable { get; }

        public float Hardness { get; }

        public float BlastResistance { get; }

        public bool RequiresTool { get; }

        public bool IsFluid { get; }

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
            bool isFluid = false)
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
        }
    }
}
