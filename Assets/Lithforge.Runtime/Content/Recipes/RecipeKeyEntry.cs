using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    [System.Serializable]
    public sealed class RecipeKeyEntry
    {
        [FormerlySerializedAs("key")]
        [Tooltip("Pattern character")]
        [SerializeField] private char _key;

        [FormerlySerializedAs("item")]
        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition _item;

        [FormerlySerializedAs("itemId")]
        [Tooltip("Item id (fallback when direct reference not set)")]
        [SerializeField] private string _itemId;

        [FormerlySerializedAs("tagId")]
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
