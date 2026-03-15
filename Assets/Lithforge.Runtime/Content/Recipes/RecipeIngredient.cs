using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// A single ingredient slot in a crafting recipe, matchable by either a specific item
    /// or any item belonging to a tag (e.g. "lithforge:planks").
    /// </summary>
    /// <remarks>
    /// Resolution priority: <see cref="Item"/> (direct SO reference) takes precedence over
    /// <see cref="ItemId"/> (string fallback), and both take precedence over <see cref="TagId"/>.
    /// When <see cref="TagId"/> is set, any item in that tag satisfies the ingredient.
    /// </remarks>
    [System.Serializable]
    public sealed class RecipeIngredient
    {
        /// <summary>Direct SO reference to the required item, or null if matched by id or tag.</summary>
        [FormerlySerializedAs("_item"),Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        /// <summary>ResourceId string fallback when the SO reference is unset.</summary>
        [FormerlySerializedAs("_itemId"),Tooltip("Item id (fallback)")]
        [SerializeField] private string itemId;

        /// <summary>Tag ResourceId allowing any member of the tag to satisfy this ingredient.</summary>
        [FormerlySerializedAs("_tagId"),Tooltip("Tag reference (alternative to item)")]
        [SerializeField] private string tagId;

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
