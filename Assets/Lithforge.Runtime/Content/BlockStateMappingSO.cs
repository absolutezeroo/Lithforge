using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBlockStateMapping", menuName = "Lithforge/Content/Block State Mapping", order = 1)]
    public sealed class BlockStateMappingSO : ScriptableObject
    {
        [Header("Variants")]
        [Tooltip("Property string → model reference mappings")]
        [SerializeField] private List<BlockStateVariantEntry> _variants = new List<BlockStateVariantEntry>();

        public IReadOnlyList<BlockStateVariantEntry> Variants
        {
            get { return _variants; }
        }
    }

    [System.Serializable]
    public sealed class BlockStateVariantEntry
    {
        [Tooltip("Variant key (e.g. '' for no properties, 'axis=y', 'facing=north,lit=false')")]
        [SerializeField] private string _variantKey = "";

        [Tooltip("Block model for this variant")]
        [SerializeField] private BlockModelSO _model;

        [Tooltip("X rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int _rotationX;

        [Tooltip("Y rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int _rotationY;

        [Tooltip("Lock UV coordinates when rotating")]
        [SerializeField] private bool _uvlock;

        [Tooltip("Selection weight for weighted random variants")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        public string VariantKey
        {
            get { return _variantKey; }
        }

        public BlockModelSO Model
        {
            get { return _model; }
        }

        public int RotationX
        {
            get { return _rotationX; }
        }

        public int RotationY
        {
            get { return _rotationY; }
        }

        public bool Uvlock
        {
            get { return _uvlock; }
        }

        public int Weight
        {
            get { return _weight; }
        }
    }
}
