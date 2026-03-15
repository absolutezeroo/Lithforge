using Lithforge.Voxel.Block;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Lookup table of mining-speed multipliers keyed by <see cref="BlockMaterialType"/>,
    /// allowing each tool type (pickaxe, axe, shovel) to mine different block materials at
    /// different rates.
    /// </summary>
    [CreateAssetMenu(fileName = "NewToolSpeedProfile",
        menuName = "Lithforge/Content/Tool Speed Profile")]
    public sealed class ToolSpeedProfile : ScriptableObject
    {
        /// <summary>
        /// Pairs a block material with the speed multiplier a tool applies when mining it.
        /// </summary>
        [System.Serializable]
        public struct MaterialSpeedEntry
        {
            /// <summary>The block material this entry applies to.</summary>
            [FormerlySerializedAs("Material")]
            public BlockMaterialType material;

            /// <summary>Multiplier on base mining speed for this material (1.0 = no change).</summary>
            [FormerlySerializedAs("SpeedMultiplier"),Min(0.01f)] public float speedMultiplier;
        }

        /// <summary>Per-material speed overrides; materials not listed default to 1.0.</summary>
        [FormerlySerializedAs("_speeds"),SerializeField] private MaterialSpeedEntry[] speeds
            = System.Array.Empty<MaterialSpeedEntry>();

        /// <summary>
        /// Returns the mining speed multiplier for the given block material,
        /// or 1.0 if no override is configured.
        /// </summary>
        /// <param name="mat">Block material to look up.</param>
        /// <returns>Speed multiplier (always >= 0.01).</returns>
        public float GetSpeed(BlockMaterialType mat)
        {
            for (int i = 0; i < speeds.Length; i++)
            {
                if (speeds[i].material == mat)
                {
                    return speeds[i].speedMultiplier;
                }
            }

            return 1.0f;
        }
    }
}
