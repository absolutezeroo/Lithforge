using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSetBlockAction", menuName = "Lithforge/Behaviors/Set Block")]
    public sealed class SetBlockAction : BehaviorAction
    {
        [Tooltip("Block to set")]
        [SerializeField] private BlockDefinition _block;

        [Tooltip("Offset from trigger block position")]
        [SerializeField] private Vector3Int _offset;

        public BlockDefinition Block
        {
            get { return _block; }
        }

        public Vector3Int Offset
        {
            get { return _offset; }
        }
    }
}
