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
        [FormerlySerializedAs("Type")]
        public MiningEffectType type;

        [FormerlySerializedAs("Value")]
        public float value;

        [FormerlySerializedAs("TargetMaterial")]
        public BlockMaterialType targetMaterial;

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
