using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class RecipeIngredient
    {
        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition _item;

        [Tooltip("Item id (fallback)")]
        [SerializeField] private string _itemId;

        [Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string _tagId;

        public ItemDefinition Item
        {
            get { return _item; }
        }

        public string ItemId
        {
            get { return _itemId; }
        }

        public string TagId
        {
            get { return _tagId; }
        }
    }
}
