using Lithforge.Voxel.Block;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Tools
{
    [CreateAssetMenu(fileName = "NewToolSpeedProfile",
        menuName = "Lithforge/Content/Tool Speed Profile")]
    public sealed class ToolSpeedProfile : ScriptableObject
    {
        [System.Serializable]
        public struct MaterialSpeedEntry
        {
            [FormerlySerializedAs("Material")]
            public BlockMaterialType material;
            [FormerlySerializedAs("SpeedMultiplier"),Min(0.01f)] public float speedMultiplier;
        }

        [FormerlySerializedAs("_speeds"),SerializeField] private MaterialSpeedEntry[] speeds
            = System.Array.Empty<MaterialSpeedEntry>();

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
