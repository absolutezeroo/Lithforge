using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.WorldGen
{
    /// <summary>
    /// Data-driven ore configuration consumed by <c>OreGenerationJob</c> to scatter ore blocks
    /// within a height band during world generation. Baked to <c>NativeOreConfig</c> at startup.
    /// </summary>
    [CreateAssetMenu(fileName = "NewOre", menuName = "Lithforge/Content/Ore Definition", order = 8)]
    public sealed class OreDefinition : ScriptableObject
    {
        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Unique name within the namespace, forming the ResourceId "namespace:oreName".</summary>
        [FormerlySerializedAs("_oreName"),Tooltip("Ore name")]
        [SerializeField] private string oreName = "";

        /// <summary>The block placed by the generator (e.g. coal_ore, iron_ore).</summary>
        [FormerlySerializedAs("_oreBlock"),Header("Blocks")]
        [Tooltip("The ore block to place")]
        [SerializeField] private BlockDefinition oreBlock;

        /// <summary>Host block the ore overwrites during generation (usually stone).</summary>
        [FormerlySerializedAs("_replaceBlock"),Tooltip("The block this ore replaces (usually stone)")]
        [SerializeField] private BlockDefinition replaceBlock;

        /// <summary>Lowest Y level at which veins can spawn.</summary>
        [FormerlySerializedAs("_minY"),Header("Generation")]
        [Tooltip("Minimum Y level")]
        [SerializeField] private int minY;

        /// <summary>Highest Y level at which veins can spawn.</summary>
        [FormerlySerializedAs("_maxY"),Tooltip("Maximum Y level")]
        [SerializeField] private int maxY = 128;

        /// <summary>Maximum number of blocks in a single vein or scatter group.</summary>
        [FormerlySerializedAs("_veinSize"),Tooltip("Maximum vein size")]
        [Min(1)]
        [SerializeField] private int veinSize = 8;

        /// <summary>Average number of vein attempts per chunk (higher = more common ore).</summary>
        [FormerlySerializedAs("_frequency"),Tooltip("Generation frequency")]
        [Min(0f)]
        [SerializeField] private float frequency = 1.0f;

        /// <summary>Whether veins form spheroid clusters (Blob) or isolated single blocks (Scatter).</summary>
        [FormerlySerializedAs("_oreType"),Tooltip("Ore generation type")]
        [SerializeField] private OreType oreType = OreType.Blob;

        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Unique name within the namespace, auto-populated from the asset name if left blank.</summary>
        public string OreName
        {
            get { return oreName; }
        }

        /// <summary>The block placed by the generator.</summary>
        public BlockDefinition OreBlock
        {
            get { return oreBlock; }
        }

        /// <summary>Host block that veins overwrite (only this block type is replaced).</summary>
        public BlockDefinition ReplaceBlock
        {
            get { return replaceBlock; }
        }

        /// <summary>Lowest Y level at which veins can spawn.</summary>
        public int MinY
        {
            get { return minY; }
        }

        /// <summary>Highest Y level at which veins can spawn.</summary>
        public int MaxY
        {
            get { return maxY; }
        }

        /// <summary>Maximum block count per vein or scatter group.</summary>
        public int VeinSize
        {
            get { return veinSize; }
        }

        /// <summary>Average vein attempts per chunk.</summary>
        public float Frequency
        {
            get { return frequency; }
        }

        /// <summary>Generation algorithm: Blob (spheroid cluster) or Scatter (individual blocks).</summary>
        public OreType OreType
        {
            get { return oreType; }
        }

        /// <summary>Editor callback that auto-fills the ore name from the asset name if empty.</summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(oreName))
            {
                oreName = name;
            }
        }
    }
}
