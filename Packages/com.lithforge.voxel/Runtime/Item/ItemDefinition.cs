using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Data-driven item definition parsed from data/{ns}/item/{id}.json.
    /// Contains all static properties of an item type.
    /// Tier 1 type — no Unity dependencies.
    /// </summary>
    public sealed class ItemDefinition
    {
        public ResourceId Id { get; }
        public int MaxStackSize { get; set; }
        public ToolType ToolType { get; set; }
        public int ToolLevel { get; set; }
        public int Durability { get; set; }
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float MiningSpeed { get; set; }
        public bool IsBlockItem { get; set; }
        public ResourceId BlockId { get; set; }
        public List<string> Tags { get; set; }

        public ItemDefinition(ResourceId id)
        {
            Id = id;
            MaxStackSize = 64;
            ToolType = ToolType.None;
            ToolLevel = 0;
            Durability = 0;
            AttackDamage = 1.0f;
            AttackSpeed = 4.0f;
            MiningSpeed = 1.0f;
            IsBlockItem = false;
            Tags = new List<string>();
        }
    }
}
