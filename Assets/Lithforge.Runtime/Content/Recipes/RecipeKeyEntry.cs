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

        /// <summary>ResourceId string for the required item, or empty if matched by tag.</summary>
        [FormerlySerializedAs("_itemId"),Tooltip("Item resource ID")]
        [SerializeField] private string itemId;

        /// <summary>Tag ResourceId; when set, any item belonging to this tag satisfies the slot.</summary>
        [FormerlySerializedAs("_tagId"),Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

        /// <summary>The character this entry maps in the pattern grid.</summary>
        public char Key
        {
            get { return key; }
        }

        /// <summary>ResourceId string for the required item, or empty if matched by tag.</summary>
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
