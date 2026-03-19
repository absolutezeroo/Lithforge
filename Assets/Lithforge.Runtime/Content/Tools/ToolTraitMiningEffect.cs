using Lithforge.Voxel.Block;
using Lithforge.Item;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// A single mining effect configured on a ToolTraitDefinition.
    /// Serializable struct for Unity Inspector editing.
    /// Converted to ToolTraitEffect (Tier 2) during content pipeline.
    /// </summary>
    [System.Serializable]
    public struct ToolTraitMiningEffect
    {
        /// <summary>Type of mining modification this effect applies.</summary>
        [FormerlySerializedAs("Type")]
        public MiningEffectType type;

        /// <summary>Magnitude of the effect (multiplier, flat bonus, or reduction amount).</summary>
        [FormerlySerializedAs("Value")]
        public float value;

        /// <summary>Block material this effect targets. None matches all materials.</summary>
        [FormerlySerializedAs("TargetMaterial")]
        public BlockMaterialType targetMaterial;

        /// <summary>Tool type this effect targets. None matches all tool types.</summary>
        [FormerlySerializedAs("TargetToolType")]
        public ToolType targetToolType;

        /// <summary>
        /// Converts this SO-serialized effect to a Tier 2 ToolTraitEffect.
        /// </summary>
        public ToolTraitEffect ToTier2()
        {
            return new ToolTraitEffect
            {
                Type = type,
                Value = value,
                TargetMaterial = targetMaterial,
                TargetToolType = targetToolType,
            };
        }
    }
}
