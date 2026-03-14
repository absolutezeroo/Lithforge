using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.WorldGen
{
    [CreateAssetMenu(fileName = "NewOre", menuName = "Lithforge/Content/Ore Definition", order = 8)]
    public sealed class OreDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("oreName")]
        [Tooltip("Ore name")]
        [SerializeField] private string _oreName = "";

        [Header("Blocks")]
        [FormerlySerializedAs("oreBlock")]
        [Tooltip("The ore block to place")]
        [SerializeField] private BlockDefinition _oreBlock;

        [FormerlySerializedAs("replaceBlock")]
        [Tooltip("The block this ore replaces (usually stone)")]
        [SerializeField] private BlockDefinition _replaceBlock;

        [Header("Generation")]
        [FormerlySerializedAs("minY")]
        [Tooltip("Minimum Y level")]
        [SerializeField] private int _minY;

        [FormerlySerializedAs("maxY")]
        [Tooltip("Maximum Y level")]
        [SerializeField] private int _maxY = 128;

        [FormerlySerializedAs("veinSize")]
        [Tooltip("Maximum vein size")]
        [Min(1)]
        [SerializeField] private int _veinSize = 8;

        [FormerlySerializedAs("frequency")]
        [Tooltip("Generation frequency")]
        [Min(0f)]
        [SerializeField] private float _frequency = 1.0f;

        [FormerlySerializedAs("oreType")]
        [Tooltip("Ore generation type")]
        [SerializeField] private OreType _oreType = OreType.Blob;

        public string Namespace
        {
            get { return @namespace; }
        }

        public string OreName
        {
            get { return _oreName; }
        }

        public BlockDefinition OreBlock
        {
            get { return _oreBlock; }
        }

        public BlockDefinition ReplaceBlock
        {
            get { return _replaceBlock; }
        }

        public int MinY
        {
            get { return _minY; }
        }

        public int MaxY
        {
            get { return _maxY; }
        }

        public int VeinSize
        {
            get { return _veinSize; }
        }

        public float Frequency
        {
            get { return _frequency; }
        }

        public OreType OreType
        {
            get { return _oreType; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_oreName))
            {
                _oreName = name;
            }
        }
    }
}
