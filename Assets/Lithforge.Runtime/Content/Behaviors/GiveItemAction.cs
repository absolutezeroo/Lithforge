using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewGiveItemAction", menuName = "Lithforge/Behaviors/Give Item")]
    public sealed class GiveItemAction : BehaviorAction
    {
        [FormerlySerializedAs("item")]
        [Tooltip("Item to give")]
        [SerializeField] private ItemDefinition _item;

        [FormerlySerializedAs("count")]
        [Tooltip("Number of items to give")]
        [Min(1)]
        [SerializeField] private int _count = 1;

        public ItemDefinition Item
        {
            get { return _item; }
        }

        public int Count
        {
            get { return _count; }
        }
    }
}
