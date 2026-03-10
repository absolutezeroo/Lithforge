using Lithforge.Runtime.Content.Items;
using UnityEngine;

namespace Lithforge.Runtime.Content.Recipes
{
    [System.Serializable]
    public sealed class RecipeIngredient
    {
        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        [Tooltip("Item id (fallback)")]
        [SerializeField] private string itemId;

        [Tooltip("Tag reference (alternative to item)")]
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
