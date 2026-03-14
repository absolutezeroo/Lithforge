using Lithforge.Runtime.Content.Models;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    [System.Serializable]
    public sealed class BlockStateVariantEntry
    {
        [FormerlySerializedAs("variantKey")]
        [Tooltip("Variant key (e.g. '' for no properties, 'axis=y', 'facing=north,lit=false')")]
        [SerializeField] private string _variantKey = "";

        [FormerlySerializedAs("model")]
        [Tooltip("Block model for this variant")]
        [SerializeField] private BlockModel _model;

        [FormerlySerializedAs("rotationX")]
        [Tooltip("X rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int _rotationX;

        [FormerlySerializedAs("rotationY")]
        [Tooltip("Y rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int _rotationY;

        [FormerlySerializedAs("uvlock")]
        [Tooltip("Lock UV coordinates when rotating")]
        [SerializeField] private bool _uvlock;

        [FormerlySerializedAs("weight")]
        [Tooltip("Selection weight for weighted random variants")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        public string VariantKey
        {
            get { return _variantKey; }
        }

        public BlockModel Model
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
