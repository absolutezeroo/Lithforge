using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    [System.Serializable]
    public sealed class RecipeIngredient
    {
        [FormerlySerializedAs("_item"),Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        [FormerlySerializedAs("_itemId"),Tooltip("Item id (fallback)")]
        [SerializeField] private string itemId;

        [FormerlySerializedAs("_tagId"),Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

        public ItemDefinition Item
        {
            get { return item; }
        }

        public string ItemId
        {
            get { return itemId; }
        }

        public string TagId
        {
            get { return tagId; }
        }
    }
}
