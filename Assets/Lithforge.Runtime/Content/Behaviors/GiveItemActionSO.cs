using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewGiveItemAction", menuName = "Lithforge/Behaviors/Give Item")]
    public sealed class GiveItemActionSO : BehaviorActionSO
    {
        [Tooltip("Item to give")]
        [SerializeField] private ItemDefinitionSO _item;

        [Tooltip("Number of items to give")]
        [Min(1)]
        [SerializeField] private int _count = 1;

        public ItemDefinitionSO Item
        {
            get { return _item; }
        }

        public int Count
        {
            get { return _count; }
        }
    }
}
