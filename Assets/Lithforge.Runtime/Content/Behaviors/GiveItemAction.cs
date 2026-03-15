using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    /// Behavior action that adds an item stack to the player's inventory when triggered.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGiveItemAction", menuName = "Lithforge/Behaviors/Give Item")]
    public sealed class GiveItemAction : BehaviorAction
    {
        /// <summary>Item definition to grant the player.</summary>
        [FormerlySerializedAs("_item"),Tooltip("Item to give")]
        [SerializeField] private ItemDefinition item;

        /// <summary>How many of this item to give per trigger.</summary>
        [FormerlySerializedAs("_count"),Tooltip("Number of items to give")]
        [Min(1)]
        [SerializeField] private int count = 1;

        /// <summary>Item definition to grant the player.</summary>
        public ItemDefinition Item
        {
            get { return item; }
        }

        /// <summary>How many of this item to give per trigger.</summary>
        public int Count
        {
            get { return count; }
        }
    }
}
