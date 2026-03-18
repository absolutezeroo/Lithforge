using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// A single ingredient slot in a crafting recipe, matchable by either a specific item
    /// or any item belonging to a tag (e.g. "lithforge:planks").
    /// </summary>
    /// <remarks>
    /// Resolution priority: <see cref="ItemId"/> takes precedence over <see cref="TagId"/>.
    /// When <see cref="TagId"/> is set, any item in that tag satisfies the ingredient.
    /// </remarks>
    [System.Serializable]
    public sealed class RecipeIngredient
    {
        /// <summary>ResourceId string for the required item, or empty if matched by tag.</summary>
        [FormerlySerializedAs("_itemId"),Tooltip("Item resource ID")]
        [SerializeField] private string itemId;

        /// <summary>Tag ResourceId allowing any member of the tag to satisfy this ingredient.</summary>
        [FormerlySerializedAs("_tagId"),Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

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
