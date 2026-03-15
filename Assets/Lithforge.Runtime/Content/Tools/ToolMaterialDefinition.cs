using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    [CreateAssetMenu(fileName = "NewToolMaterial",
        menuName = "Lithforge/Content/Tool Material Definition")]
    public sealed class ToolMaterialDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string materialId;

        [Header("Compatible Parts")]
        public ToolPartType[] compatibleParts;

        [Header("Head / Blade Stats")]
        [Min(0.1f)] public float headMiningSpeed = 1f;
        [Min(1)] public int headDurability = 100;
        [Min(0f)] public float headAttackDamage = 1f;

        [Header("Handle Stats")]
        [Min(0.1f)] public float handleDurabilityMultiplier = 1f;
        [Min(0.1f)] public float handleSpeedMultiplier = 1f;

        [Header("Binding / Guard Bonus")]
        [Min(0)] public int bindingDurabilityBonus = 0;

        [Header("Traits")]
        public string[] traitIds = System.Array.Empty<string>();

        [Header("Tool Level")]
        [Min(0)] public int toolLevel = 0;
    }
}
