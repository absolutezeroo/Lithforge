using System.Collections.Generic;
using Lithforge.Runtime.Content.Behaviors;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Pairs a trigger event with a list of actions that fire when that event occurs on a block.
    /// Attach to a <see cref="BlockDefinition"/> to give blocks interactive or reactive behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlockBehavior", menuName = "Lithforge/Content/Block Behavior", order = 9)]
    public sealed class BlockBehavior : ScriptableObject
    {
        /// <summary>Event that causes the attached actions to execute.</summary>
        [FormerlySerializedAs("_trigger"),Header("Trigger")]
        [Tooltip("When this behavior is triggered")]
        [SerializeField] private BlockBehaviorTrigger trigger;

        /// <summary>Ordered list of actions to run when the trigger fires. Executed sequentially.</summary>
        [FormerlySerializedAs("_actions"),Header("Actions")]
        [Tooltip("Actions to execute when triggered")]
        [SerializeField] private List<BehaviorAction> actions = new List<BehaviorAction>();

        /// <summary>Event that causes the attached actions to execute.</summary>
        public BlockBehaviorTrigger Trigger
        {
            get { return trigger; }
        }

        /// <summary>Ordered list of actions to run when the trigger fires.</summary>
        public IReadOnlyList<BehaviorAction> Actions
        {
            get { return actions; }
        }
    }

    /// <summary>
    /// Events that can activate a <see cref="BlockBehavior"/>.
    /// </summary>
    public enum BlockBehaviorTrigger
    {
        /// <summary>Fires when the block is broken by mining or explosion.</summary>
        OnBreak = 0,
        /// <summary>Fires when the block is placed by a player.</summary>
        OnPlace = 1,
        /// <summary>Fires when a player right-clicks (interacts with) the block.</summary>
        OnInteract = 2,
        /// <summary>Fires when an adjacent block changes (placed, removed, or state update).</summary>
        OnNeighborUpdate = 3,
        /// <summary>Fires on random tick — used for growth, decay, or ambient effects.</summary>
        OnRandomTick = 4,
        /// <summary>Fires when an entity walks on the block's top face.</summary>
        OnSteppedOn = 5,
    }
}
