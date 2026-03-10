using System.Collections.Generic;
using Lithforge.Voxel.Crafting;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Lithforge/Content/Recipe Definition", order = 4)]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Recipe name")]
        [SerializeField] private string _recipeName = "";

        [Header("Type")]
        [SerializeField] private RecipeType _type = RecipeType.Shaped;

        [Header("Result")]
        [Tooltip("Result item")]
        [SerializeField] private ItemDefinition _resultItem;

        [Tooltip("Result item id (fallback when direct reference not set)")]
        [SerializeField] private string _resultItemId;

        [Tooltip("Number of items produced")]
        [Min(1)]
        [SerializeField] private int _resultCount = 1;

        [Header("Shaped Pattern")]
        [Tooltip("Pattern rows (e.g. '## ', '## ', '   ')")]
        [SerializeField] private List<string> _pattern = new List<string>();

        [Tooltip("Key mappings (character → item)")]
        [SerializeField] private List<RecipeKeyEntry> _keys = new List<RecipeKeyEntry>();

        [Header("Shapeless Ingredients")]
        [Tooltip("Ingredients for shapeless recipes")]
        [SerializeField] private List<RecipeIngredient> _ingredients = new List<RecipeIngredient>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string RecipeName
        {
            get { return _recipeName; }
        }

        public RecipeType Type
        {
            get { return _type; }
        }

        public ItemDefinition ResultItem
        {
            get { return _resultItem; }
        }

        public string ResultItemId
        {
            get { return _resultItemId; }
        }

        public int ResultCount
        {
            get { return _resultCount; }
        }

        public IReadOnlyList<string> Pattern
        {
            get { return _pattern; }
        }

        public IReadOnlyList<RecipeKeyEntry> Keys
        {
            get { return _keys; }
        }

        public IReadOnlyList<RecipeIngredient> Ingredients
        {
            get { return _ingredients; }
        }
    }

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
