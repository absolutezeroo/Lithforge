using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// Binds a single character in a shaped recipe pattern to the ingredient it represents.
    /// For example, key '#' might map to <c>lithforge:oak_planks</c>.
    /// </summary>
    [System.Serializable]
    public sealed class RecipeKeyEntry
    {
        /// <summary>The character used in <see cref="RecipeDefinition.Pattern"/> rows (space is always empty).</summary>
        [FormerlySerializedAs("_key"),Tooltip("Pattern character")]
        [SerializeField] private char key;

        /// <summary>Direct SO reference to the required item, or null if matched by id or tag.</summary>
        [FormerlySerializedAs("_item"),Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        /// <summary>ResourceId string fallback when the SO reference is unset.</summary>
        [FormerlySerializedAs("_itemId"),Tooltip("Item id (fallback when direct reference not set)")]
        [SerializeField] private string itemId;

        /// <summary>Tag ResourceId; when set, any item belonging to this tag satisfies the slot.</summary>
        [FormerlySerializedAs("_tagId"),Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

        /// <summary>The character this entry maps in the pattern grid.</summary>
        public char Key
        {
            get { return key; }
        }

        /// <summary>Direct SO reference to the required item, or null if matched by id or tag.</summary>
        public ItemDefinition Item
        {
            get { return item; }
        }

        /// <summary>ResourceId string fallback when the SO reference is unset.</summary>
        public string ItemId
        {
            get { return itemId; }
        }

        /// <summary>Tag ResourceId; when set, any item belonging to this tag satisfies the slot.</summary>
        public string TagId
        {
            get { return tagId; }
        }
    }
}
