using System.Collections.Generic;
using Lithforge.Voxel.Crafting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// Data-driven crafting recipe, authored as a ScriptableObject and converted to a
    /// Tier 2 recipe at startup by <c>RecipeLoader</c> for use by <c>CraftingEngine</c>.
    /// Supports both shaped (pattern grid + key map) and shapeless (unordered ingredient list) modes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Lithforge/Content/Recipe Definition", order = 4)]
    public sealed class RecipeDefinition : ScriptableObject
    {
        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Unique name within the namespace, forming the ResourceId "namespace:recipeName".</summary>
        [FormerlySerializedAs("_recipeName"),Tooltip("Recipe name")]
        [SerializeField] private string recipeName = "";

        /// <summary>Whether the grid arrangement matters (Shaped) or only the ingredient set (Shapeless).</summary>
        [FormerlySerializedAs("_type"),Header("Type")]
        [SerializeField] private RecipeType type = RecipeType.Shaped;

        /// <summary>ResourceId string for the item produced by this recipe (e.g. "lithforge:oak_planks").</summary>
        [FormerlySerializedAs("_resultItemId"),Header("Result")]
        [Tooltip("Result item resource ID (e.g. lithforge:oak_planks)")]
        [SerializeField] private string resultItemId;

        /// <summary>Stack size of the crafting output per single craft.</summary>
        [FormerlySerializedAs("_resultCount"),Tooltip("Number of items produced")]
        [Min(1)]
        [SerializeField] private int resultCount = 1;

        /// <summary>
        /// Row strings defining the crafting grid layout for shaped recipes.
        /// Each character maps to a <see cref="RecipeKeyEntry"/>; spaces represent empty slots.
        /// </summary>
        [FormerlySerializedAs("_pattern"),Header("Shaped Pattern")]
        [Tooltip("Pattern rows (e.g. '## ', '## ', '   ')")]
        [SerializeField] private List<string> pattern = new();

        /// <summary>Maps each character used in <see cref="pattern"/> to an ingredient.</summary>
        [FormerlySerializedAs("_keys"),Tooltip("Key mappings (character → item)")]
        [SerializeField] private List<RecipeKeyEntry> keys = new();

        /// <summary>Unordered ingredient list used when <see cref="type"/> is <see cref="RecipeType.Shapeless"/>.</summary>
        [FormerlySerializedAs("_ingredients"),Header("Shapeless Ingredients")]
        [Tooltip("Ingredients for shapeless recipes")]
        [SerializeField] private List<RecipeIngredient> ingredients = new();

        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Unique name within the namespace, auto-populated from the asset name if left blank.</summary>
        public string RecipeName
        {
            get { return recipeName; }
        }

        /// <summary>Shaped or Shapeless crafting mode.</summary>
        public RecipeType Type
        {
            get { return type; }
        }

        /// <summary>ResourceId string for the produced item (e.g. "lithforge:oak_planks").</summary>
        public string ResultItemId
        {
            get { return resultItemId; }
        }

        /// <summary>How many items a single craft produces.</summary>
        public int ResultCount
        {
            get { return resultCount; }
        }

        /// <summary>Row strings for the shaped grid layout; only used when <see cref="Type"/> is Shaped.</summary>
        public IReadOnlyList<string> Pattern
        {
            get { return pattern; }
        }

        /// <summary>Character-to-ingredient mappings referenced by <see cref="Pattern"/>.</summary>
        public IReadOnlyList<RecipeKeyEntry> Keys
        {
            get { return keys; }
        }

        /// <summary>Unordered ingredients for shapeless recipes.</summary>
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
