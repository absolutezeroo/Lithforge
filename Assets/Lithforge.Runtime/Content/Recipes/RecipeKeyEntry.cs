using Lithforge.Runtime.Content.Items;
using UnityEngine;

namespace Lithforge.Runtime.Content.Recipes
{
    [System.Serializable]
    public sealed class RecipeKeyEntry
    {
        [Tooltip("Pattern character")]
        [SerializeField] private char key;

        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        [Tooltip("Item id (fallback when direct reference not set)")]
        [SerializeField] private string itemId;

        [Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

        public char Key
        {
            get { return key; }
        }

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
