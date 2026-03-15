using Lithforge.Runtime.Content.Blocks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    /// Behavior action that places or replaces a block at an offset from the trigger position.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSetBlockAction", menuName = "Lithforge/Behaviors/Set Block")]
    public sealed class SetBlockAction : BehaviorAction
    {
        /// <summary>Block type to place in the world.</summary>
        [FormerlySerializedAs("_block"),Tooltip("Block to set")]
        [SerializeField] private BlockDefinition block;

        /// <summary>Position offset from the triggering block (e.g. (0,1,0) = one block above).</summary>
        [FormerlySerializedAs("_offset"),Tooltip("Offset from trigger block position")]
        [SerializeField] private Vector3Int offset;

        /// <summary>Block type to place in the world.</summary>
        public BlockDefinition Block
        {
            get { return block; }
        }

        /// <summary>Position offset from the triggering block (e.g. (0,1,0) = one block above).</summary>
        public Vector3Int Offset
        {
            get { return offset; }
        }
    }
}
