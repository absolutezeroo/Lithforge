using Lithforge.Runtime.Content.Models;
using UnityEngine;

namespace Lithforge.Runtime.Content.Blocks
{
    [System.Serializable]
    public sealed class BlockStateVariantEntry
    {
        [Tooltip("Variant key (e.g. '' for no properties, 'axis=y', 'facing=north,lit=false')")]
        [SerializeField] private string variantKey = "";

        [Tooltip("Block model for this variant")]
        [SerializeField] private BlockModel model;

        [Tooltip("X rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int rotationX;

        [Tooltip("Y rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int rotationY;

        [Tooltip("Lock UV coordinates when rotating")]
        [SerializeField] private bool uvlock;

        [Tooltip("Selection weight for weighted random variants")]
        [Min(1)]
        [SerializeField] private int weight = 1;

        public string VariantKey
        {
            get { return variantKey; }
        }

        public BlockModel Model
        {
            get { return model; }
        }

        public int RotationX
        {
            get { return rotationX; }
        }

        public int RotationY
        {
            get { return rotationY; }
        }

        public bool Uvlock
        {
            get { return uvlock; }
        }

        public int Weight
        {
            get { return weight; }
        }
    }
}
