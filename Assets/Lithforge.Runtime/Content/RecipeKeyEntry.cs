using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class RecipeKeyEntry
    {
        [Tooltip("Pattern character")]
        [SerializeField] private char _key;

        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition _item;

        [Tooltip("Item id (fallback when direct reference not set)")]
        [SerializeField] private string _itemId;

        [Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string _tagId;

        public char Key
        {
            get { return _key; }
        }

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
