using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBlockStateMapping", menuName = "Lithforge/Content/Block State Mapping", order = 1)]
    public sealed class BlockStateMapping : ScriptableObject
    {
        [Header("Variants")]
        [Tooltip("Property string → model reference mappings")]
        [SerializeField] private List<BlockStateVariantEntry> _variants = new List<BlockStateVariantEntry>();

        public IReadOnlyList<BlockStateVariantEntry> Variants
        {
            get { return _variants; }
        }
    }
}
