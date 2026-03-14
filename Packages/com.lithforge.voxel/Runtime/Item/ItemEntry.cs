using System;
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

        /// <summary>
        /// Fuel burn time in seconds. 0 = not a fuel item.
        /// Used by FuelBurnBehavior in furnace block entities.
        /// </summary>
        public float FuelTime { get; set; }

        /// <summary>
        /// Tool speed profile (stored as object to avoid Tier 3 dependency).
        /// Cast to ToolSpeedProfile in Tier 3 consuming code.
        /// </summary>
        public object ToolSpeedProfile { get; set; }

        /// <summary>
        /// Mining modifiers from affixes/enchantments applied to this item.
        /// </summary>
        public IMiningModifier[] Modifiers { get; set; }

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
            Modifiers = Array.Empty<IMiningModifier>();
        }
    }
}
