using System.Collections.Generic;
using Lithforge.Runtime.Content.Behaviors;
using UnityEngine;

namespace Lithforge.Runtime.Content.Blocks
{
    [CreateAssetMenu(fileName = "NewBlockBehavior", menuName = "Lithforge/Content/Block Behavior", order = 9)]
    public sealed class BlockBehavior : ScriptableObject
    {
        [Header("Trigger")]
        [Tooltip("When this behavior is triggered")]
        [SerializeField] private BlockBehaviorTrigger trigger;

        [Header("Actions")]
        [Tooltip("Actions to execute when triggered")]
        [SerializeField] private List<BehaviorAction> actions = new List<BehaviorAction>();

        public BlockBehaviorTrigger Trigger
        {
            get { return trigger; }
        }

        public IReadOnlyList<BehaviorAction> Actions
        {
            get { return actions; }
        }
    }

    public enum BlockBehaviorTrigger
    {
        OnBreak = 0,
        OnPlace = 1,
        OnInteract = 2,
        OnNeighborUpdate = 3,
        OnRandomTick = 4,
        OnSteppedOn = 5,
    }
}
