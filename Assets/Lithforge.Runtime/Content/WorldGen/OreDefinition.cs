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

        [FormerlySerializedAs("_oreName"),Tooltip("Ore name")]
        [SerializeField] private string oreName = "";

        [FormerlySerializedAs("_oreBlock"),Header("Blocks")]
        [Tooltip("The ore block to place")]
        [SerializeField] private BlockDefinition oreBlock;

        [FormerlySerializedAs("_replaceBlock"),Tooltip("The block this ore replaces (usually stone)")]
        [SerializeField] private BlockDefinition replaceBlock;

        [FormerlySerializedAs("_minY"),Header("Generation")]
        [Tooltip("Minimum Y level")]
        [SerializeField] private int minY;

        [FormerlySerializedAs("_maxY"),Tooltip("Maximum Y level")]
        [SerializeField] private int maxY = 128;

        [FormerlySerializedAs("_veinSize"),Tooltip("Maximum vein size")]
        [Min(1)]
        [SerializeField] private int veinSize = 8;

        [FormerlySerializedAs("_frequency"),Tooltip("Generation frequency")]
        [Min(0f)]
        [SerializeField] private float frequency = 1.0f;

        [FormerlySerializedAs("_oreType"),Tooltip("Ore generation type")]
        [SerializeField] private OreType oreType = OreType.Blob;

        public string Namespace
        {
            get { return @namespace; }
        }

        public string OreName
        {
            get { return oreName; }
        }

        public BlockDefinition OreBlock
        {
            get { return oreBlock; }
        }

        public BlockDefinition ReplaceBlock
        {
            get { return replaceBlock; }
        }

        public int MinY
        {
            get { return minY; }
        }

        public int MaxY
        {
            get { return maxY; }
        }

        public int VeinSize
        {
            get { return veinSize; }
        }

        public float Frequency
        {
            get { return frequency; }
        }

        public OreType OreType
        {
            get { return oreType; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(oreName))
            {
                oreName = name;
            }
        }
    }
}
