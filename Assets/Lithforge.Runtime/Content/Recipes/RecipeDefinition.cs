using System.Collections.Generic;
using Lithforge.Runtime.Content.Items;
using Lithforge.Voxel.Crafting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Lithforge/Content/Recipe Definition", order = 4)]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("recipeName")]
        [Tooltip("Recipe name")]
        [SerializeField] private string _recipeName = "";

        [FormerlySerializedAs("type")]
        [Header("Type")]
        [SerializeField] private RecipeType _type = RecipeType.Shaped;

        [FormerlySerializedAs("resultItem")]
        [Header("Result")]
        [Tooltip("Result item")]
        [SerializeField] private ItemDefinition _resultItem;

        [FormerlySerializedAs("resultItemId")]
        [Tooltip("Result item id (fallback when direct reference not set)")]
        [SerializeField] private string _resultItemId;

        [FormerlySerializedAs("resultCount")]
        [Tooltip("Number of items produced")]
        [Min(1)]
        [SerializeField] private int _resultCount = 1;

        [FormerlySerializedAs("pattern")]
        [Header("Shaped Pattern")]
        [Tooltip("Pattern rows (e.g. '## ', '## ', '   ')")]
        [SerializeField] private List<string> _pattern = new List<string>();

        [FormerlySerializedAs("keys")]
        [Tooltip("Key mappings (character → item)")]
        [SerializeField] private List<RecipeKeyEntry> _keys = new List<RecipeKeyEntry>();

        [FormerlySerializedAs("ingredients")]
        [Header("Shapeless Ingredients")]
        [Tooltip("Ingredients for shapeless recipes")]
        [SerializeField] private List<RecipeIngredient> _ingredients = new List<RecipeIngredient>();

        public string Namespace
        {
            get { return @namespace; }
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

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_recipeName))
            {
                _recipeName = name;
            }
        }
    }
}
