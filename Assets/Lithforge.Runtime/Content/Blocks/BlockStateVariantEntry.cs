using Lithforge.Runtime.Content.Models;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// A single variant in a <see cref="BlockStateMapping"/> — ties a property combination string
    /// to a model reference with rotation and UV-lock settings.
    /// </summary>
    [System.Serializable]
    public sealed class BlockStateVariantEntry
    {
        /// <summary>Comma-separated property string (e.g. "axis=y", "facing=north,lit=false"). Empty = default/no-property state.</summary>
        [FormerlySerializedAs("_variantKey"),Tooltip("Variant key (e.g. '' for no properties, 'axis=y', 'facing=north,lit=false')")]
        [SerializeField] private string variantKey = "";

        /// <summary>Block model asset used for this variant's mesh generation.</summary>
        [FormerlySerializedAs("_model"),Tooltip("Block model for this variant")]
        [SerializeField] private BlockModel model;

        /// <summary>X-axis rotation in degrees (0, 90, 180, 270). Applied during mesh generation.</summary>
        [FormerlySerializedAs("_rotationX"),Tooltip("X rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int rotationX;

        /// <summary>Y-axis rotation in degrees (0, 90, 180, 270). Applied during mesh generation.</summary>
        [FormerlySerializedAs("_rotationY"),Tooltip("Y rotation in degrees (0, 90, 180, 270)")]
        [SerializeField] private int rotationY;

        /// <summary>When true, UV coordinates stay world-aligned instead of rotating with the model.</summary>
        [FormerlySerializedAs("_uvlock"),Tooltip("Lock UV coordinates when rotating")]
        [SerializeField] private bool uvlock;

        /// <summary>Selection weight for weighted-random variant picking. Higher = more likely.</summary>
        [FormerlySerializedAs("_weight"),Tooltip("Selection weight for weighted random variants")]
        [Min(1)]
        [SerializeField] private int weight = 1;

        /// <summary>Comma-separated property string (e.g. "axis=y", "facing=north,lit=false"). Empty = default/no-property state.</summary>
        public string VariantKey
        {
            get { return variantKey; }
        }

        /// <summary>Block model asset used for this variant's mesh generation.</summary>
        public BlockModel Model
        {
            get { return model; }
        }

        /// <summary>X-axis rotation in degrees (0, 90, 180, 270). Applied during mesh generation.</summary>
        public int RotationX
        {
            get { return rotationX; }
        }

        /// <summary>Y-axis rotation in degrees (0, 90, 180, 270). Applied during mesh generation.</summary>
        public int RotationY
        {
            get { return rotationY; }
        }

        /// <summary>When true, UV coordinates stay world-aligned instead of rotating with the model.</summary>
        public bool Uvlock
        {
            get { return uvlock; }
        }

        /// <summary>Selection weight for weighted-random variant picking. Higher = more likely.</summary>
        public int Weight
        {
            get { return weight; }
        }
    }
}
