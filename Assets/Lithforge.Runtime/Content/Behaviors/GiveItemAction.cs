using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewGiveItemAction", menuName = "Lithforge/Behaviors/Give Item")]
    public sealed class GiveItemAction : BehaviorAction
    {
        [Tooltip("Item to give")]
        [SerializeField] private ItemDefinition item;

        [Tooltip("Number of items to give")]
        [Min(1)]
        [SerializeField] private int count = 1;

        public ItemDefinition Item
        {
            get { return item; }
        }

        public int Count
        {
            get { return count; }
        }
    }
}
