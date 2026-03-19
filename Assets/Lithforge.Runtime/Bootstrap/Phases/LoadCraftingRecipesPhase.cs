using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.Content.Recipes;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 11: Loads RecipeDefinition ScriptableObjects and builds the CraftingEngine.</summary>
    public sealed class LoadCraftingRecipesPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading recipes...";
            }
        }

        /// <summary>Loads recipe assets and converts them into RecipeEntry objects for the CraftingEngine.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            RecipeDefinition[] recipeAssets =
                Resources.LoadAll<RecipeDefinition>("Content/Recipes");
            List<RecipeEntry> recipes = new();

            for (int i = 0; i < recipeAssets.Length; i++)
            {
                RecipeEntry recipeDef = ConvertRecipe(recipeAssets[i]);
                recipes.Add(recipeDef);
            }

            ctx.CraftingEngine = new CraftingEngine(recipes);
            ctx.Logger.LogInfo($"Loaded {recipes.Count} crafting recipes.");
        }

        /// <summary>Converts a RecipeDefinition ScriptableObject to a runtime RecipeEntry.</summary>
        private static RecipeEntry ConvertRecipe(RecipeDefinition source)
        {
            ResourceId id = new(source.Namespace, source.RecipeName);
            RecipeEntry recipe = new(id)
            {
                Type = source.Type, ResultCount = source.ResultCount,
            };

            if (!string.IsNullOrEmpty(source.ResultItemId))
            {
                recipe.ResultItem = ResourceId.Parse(source.ResultItemId);
            }

            IReadOnlyList<string> pattern = source.Pattern;

            for (int i = 0; i < pattern.Count; i++)
            {
                recipe.Pattern.Add(pattern[i]);
            }

            IReadOnlyList<RecipeKeyEntry> keys = source.Keys;

            for (int i = 0; i < keys.Count; i++)
            {
                RecipeKeyEntry key = keys[i];

                if (!string.IsNullOrEmpty(key.ItemId))
                {
                    recipe.Keys[key.Key] = ResourceId.Parse(key.ItemId);
                }
            }

            IReadOnlyList<RecipeIngredient> ingredients = source.Ingredients;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!string.IsNullOrEmpty(ingredients[i].ItemId))
                {
                    recipe.Ingredients.Add(ResourceId.Parse(ingredients[i].ItemId));
                }
            }

            return recipe;
        }
    }
}
