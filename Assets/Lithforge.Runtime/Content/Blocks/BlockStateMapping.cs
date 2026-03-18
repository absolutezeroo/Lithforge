using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    ///     Maps block property combinations to visual representations (models + rotations).
    ///     Each variant entry matches a property key string (e.g. "axis=y", "facing=north,lit=true")
    ///     to a <see cref="Models.BlockModel" /> with optional rotation and UV-lock.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlockStateMapping", menuName = "Lithforge/Content/Block State Mapping", order = 1)]
    public sealed class BlockStateMapping : ScriptableObject
    {
        /// <summary>List of property-key → model mappings. Resolved during ContentPipeline Phase 2.</summary>
        [FormerlySerializedAs("_variants"), Header("Variants"), Tooltip("Property string → model reference mappings"), SerializeField]
         private List<BlockStateVariantEntry> variants = new();

        /// <summary>List of property-key → model mappings.</summary>
        public IReadOnlyList<BlockStateVariantEntry> Variants
        {
            get { return variants; }
        }
    }
}
