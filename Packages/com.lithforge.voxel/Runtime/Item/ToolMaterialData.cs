using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Resolved tool material data for runtime use.
    /// Built from ToolMaterialDefinition during content pipeline.
    /// </summary>
    public sealed class ToolMaterialData
    {
        public ResourceId MaterialId { get; }
        public ToolPartType[] CompatibleParts { get; }
        public float HeadMiningSpeed { get; }
        public int HeadDurability { get; }
        public float HeadAttackDamage { get; }
        public float HandleDurabilityMultiplier { get; }
        public float HandleSpeedMultiplier { get; }
        public int BindingDurabilityBonus { get; }
        public string[] TraitIds { get; }
        public int ToolLevel { get; }

        public ToolMaterialData(
            ResourceId materialId,
            ToolPartType[] compatibleParts,
            float headMiningSpeed,
            int headDurability,
            float headAttackDamage,
            float handleDurabilityMultiplier,
            float handleSpeedMultiplier,
            int bindingDurabilityBonus,
            string[] traitIds,
            int toolLevel)
        {
            MaterialId = materialId;
            CompatibleParts = compatibleParts;
            HeadMiningSpeed = headMiningSpeed;
            HeadDurability = headDurability;
            HeadAttackDamage = headAttackDamage;
            HandleDurabilityMultiplier = handleDurabilityMultiplier;
            HandleSpeedMultiplier = handleSpeedMultiplier;
            BindingDurabilityBonus = bindingDurabilityBonus;
            TraitIds = traitIds;
            ToolLevel = toolLevel;
        }
    }
}
