using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewOre", menuName = "Lithforge/Content/Ore Definition", order = 8)]
    public sealed class OreDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Ore name")]
        [SerializeField] private string _oreName = "";

        [Header("Blocks")]
        [Tooltip("The ore block to place")]
        [SerializeField] private BlockDefinitionSO _oreBlock;

        [Tooltip("The block this ore replaces (usually stone)")]
        [SerializeField] private BlockDefinitionSO _replaceBlock;

        [Header("Generation")]
        [Tooltip("Minimum Y level")]
        [SerializeField] private int _minY;

        [Tooltip("Maximum Y level")]
        [SerializeField] private int _maxY = 128;

        [Tooltip("Maximum vein size")]
        [Min(1)]
        [SerializeField] private int _veinSize = 8;

        [Tooltip("Generation frequency")]
        [Min(0f)]
        [SerializeField] private float _frequency = 1.0f;

        [Tooltip("Ore generation type")]
        [SerializeField] private OreType _oreType = OreType.Blob;

        public string Namespace
        {
            get { return _namespace; }
        }

        public string OreName
        {
            get { return _oreName; }
        }

        public BlockDefinitionSO OreBlock
        {
            get { return _oreBlock; }
        }

        public BlockDefinitionSO ReplaceBlock
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
    }

    public enum OreType
    {
        Blob = 0,
        Scatter = 1,
    }
}
