using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Resolved item entry for the runtime item registry.
    /// Built from ScriptableObject data by the ContentPipeline.
    /// Tier 2 type — no UnityEngine dependencies.
    /// </summary>
    public sealed class ItemEntry
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

        public ItemEntry(ResourceId id)
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
