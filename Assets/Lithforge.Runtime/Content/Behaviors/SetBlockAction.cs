using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSetBlockAction", menuName = "Lithforge/Behaviors/Set Block")]
    public sealed class SetBlockAction : BehaviorAction
    {
        [FormerlySerializedAs("block")]
        [Tooltip("Block to set")]
        [SerializeField] private BlockDefinition _block;

        [FormerlySerializedAs("offset")]
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
