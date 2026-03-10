using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSetBlockAction", menuName = "Lithforge/Behaviors/Set Block")]
    public sealed class SetBlockActionSO : BehaviorActionSO
    {
        [Tooltip("Block to set")]
        [SerializeField] private BlockDefinitionSO _block;

        [Tooltip("Offset from trigger block position")]
        [SerializeField] private Vector3Int _offset;

        public BlockDefinitionSO Block
        {
            get { return _block; }
        }

        public Vector3Int Offset
        {
            get { return _offset; }
        }
    }
}
