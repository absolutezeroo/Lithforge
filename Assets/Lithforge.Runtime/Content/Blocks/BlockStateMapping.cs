using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    [CreateAssetMenu(fileName = "NewBlockStateMapping", menuName = "Lithforge/Content/Block State Mapping", order = 1)]
    public sealed class BlockStateMapping : ScriptableObject
    {
        [FormerlySerializedAs("_variants"),Header("Variants")]
        [Tooltip("Property string → model reference mappings")]
        [SerializeField] private List<BlockStateVariantEntry> variants = new List<BlockStateVariantEntry>();

        public IReadOnlyList<BlockStateVariantEntry> Variants
        {
            get { return variants; }
        }
    }
}
