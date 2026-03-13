using Lithforge.Voxel.Block;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    [CreateAssetMenu(fileName = "NewToolSpeedProfile",
        menuName = "Lithforge/Content/Tool Speed Profile")]
    public sealed class ToolSpeedProfileSO : ScriptableObject
    {
        [System.Serializable]
        public struct MaterialSpeedEntry
        {
            public BlockMaterialType Material;
            [Min(0.01f)] public float SpeedMultiplier;
        }

        [SerializeField] private MaterialSpeedEntry[] _speeds
            = System.Array.Empty<MaterialSpeedEntry>();

        public float GetSpeed(BlockMaterialType mat)
        {
            for (int i = 0; i < _speeds.Length; i++)
            {
                if (_speeds[i].Material == mat)
                {
                    return _speeds[i].SpeedMultiplier;
                }
            }

            return 1.0f;
        }
    }
}
