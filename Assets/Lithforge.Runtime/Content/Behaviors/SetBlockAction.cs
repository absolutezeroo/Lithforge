using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSetBlockAction", menuName = "Lithforge/Behaviors/Set Block")]
    public sealed class SetBlockAction : BehaviorAction
    {
        [Tooltip("Block to set")]
        [SerializeField] private BlockDefinition block;

        [Tooltip("Offset from trigger block position")]
        [SerializeField] private Vector3Int offset;

        public BlockDefinition Block
        {
            get { return block; }
        }

        public Vector3Int Offset
        {
            get { return offset; }
        }
    }
}
