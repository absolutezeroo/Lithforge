using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Data-driven block definition parsed from data/{ns}/blocks/{id}.json.
    /// Contains all static properties of a block type.
    /// Tier 1 type — no Unity dependencies in this file.
    /// </summary>
    public sealed class BlockDefinition
    {
        public ResourceId Id { get; }
        public double Hardness { get; set; }
        public double BlastResistance { get; set; }
        public bool RequiresTool { get; set; }
        public string SoundGroup { get; set; }
        public string CollisionShape { get; set; }
        public string RenderLayer { get; set; }
        public int LightEmission { get; set; }
        public int LightFilter { get; set; }
        public string MapColor { get; set; }
        public string LootTable { get; set; }
        public List<string> Tags { get; set; }
        public IReadOnlyList<PropertyDefinition> Properties { get; }

        public BlockDefinition(ResourceId id, IReadOnlyList<PropertyDefinition> properties)
        {
            Id = id;
            Properties = properties ?? new List<PropertyDefinition>();
            Hardness = 1.0;
            BlastResistance = 1.0;
            RequiresTool = false;
            SoundGroup = "stone";
            CollisionShape = "full_cube";
            RenderLayer = "opaque";
            LightEmission = 0;
            LightFilter = 15;
            Tags = new List<string>();
        }

        /// <summary>
        /// Computes the total number of block states (cartesian product of all property values).
        /// A block with no properties has exactly 1 state.
        /// </summary>
        public int ComputeStateCount()
        {
            if (Properties.Count == 0)
            {
                return 1;
            }

            int count = 1;

            for (int i = 0; i < Properties.Count; i++)
            {
                count *= Properties[i].ValueCount;
            }

            return count;
        }
    }
}
