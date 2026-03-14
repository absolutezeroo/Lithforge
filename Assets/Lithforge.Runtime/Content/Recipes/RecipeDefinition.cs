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

        [Tooltip("Recipe name")]
        [SerializeField] private string recipeName = "";

        [Header("Type")]
        [SerializeField] private RecipeType type = RecipeType.Shaped;

        [Header("Result")]
        [Tooltip("Result item")]
        [SerializeField] private ItemDefinition resultItem;

        [Tooltip("Result item id (fallback when direct reference not set)")]
        [SerializeField] private string resultItemId;

        [Tooltip("Number of items produced")]
        [Min(1)]
        [SerializeField] private int resultCount = 1;

        [Header("Shaped Pattern")]
        [Tooltip("Pattern rows (e.g. '## ', '## ', '   ')")]
        [SerializeField] private List<string> pattern = new List<string>();

        [Tooltip("Key mappings (character → item)")]
        [SerializeField] private List<RecipeKeyEntry> keys = new List<RecipeKeyEntry>();

        [Header("Shapeless Ingredients")]
        [Tooltip("Ingredients for shapeless recipes")]
        [SerializeField] private List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        public string Namespace
        {
            get { return @namespace; }
        }

        public string RecipeName
        {
            get { return recipeName; }
        }

        public RecipeType Type
        {
            get { return type; }
        }

        public ItemDefinition ResultItem
        {
            get { return resultItem; }
        }

        public string ResultItemId
        {
            get { return resultItemId; }
        }

        public int ResultCount
        {
            get { return resultCount; }
        }

        public IReadOnlyList<string> Pattern
        {
            get { return pattern; }
        }

        public IReadOnlyList<RecipeKeyEntry> Keys
        {
            get { return keys; }
        }

        public IReadOnlyList<RecipeIngredient> Ingredients
        {
            get { return ingredients; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(recipeName))
            {
                recipeName = name;
            }
        }
    }
}
