using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Matches a CraftingGrid against registered recipes.
    /// Supports both shaped (pattern matching with offset) and shapeless recipes.
    /// </summary>
    public sealed class CraftingEngine
    {
        private readonly List<RecipeDefinition> _recipes;

        public CraftingEngine(List<RecipeDefinition> recipes)
        {
            _recipes = recipes;
        }

        /// <summary>
        /// Finds the first recipe that matches the current grid contents.
        /// Returns null if no recipe matches.
        /// </summary>
        public RecipeDefinition FindMatch(CraftingGrid grid)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                RecipeDefinition recipe = _recipes[i];

                if (recipe.Type == RecipeType.Shaped)
                {
                    if (MatchesShaped(grid, recipe))
                    {
                        return recipe;
                    }
                }
                else if (recipe.Type == RecipeType.Shapeless)
                {
                    if (MatchesShapeless(grid, recipe))
                    {
                        return recipe;
                    }
                }
            }

            return null;
        }

        private bool MatchesShaped(CraftingGrid grid, RecipeDefinition recipe)
        {
            if (recipe.Pattern.Count == 0)
            {
                return false;
            }

            // Get bounds of non-empty slots in grid
            grid.GetBounds(out int gridMinX, out int gridMinY, out int gridW, out int gridH);

            // Get recipe dimensions
            int recipeH = recipe.Pattern.Count;
            int recipeW = 0;

            for (int r = 0; r < recipe.Pattern.Count; r++)
            {
                if (recipe.Pattern[r].Length > recipeW)
                {
                    recipeW = recipe.Pattern[r].Length;
                }
            }

            // Dimensions must match
            if (gridW != recipeW || gridH != recipeH)
            {
                return false;
            }

            // Check each cell
            for (int y = 0; y < recipeH; y++)
            {
                string row = recipe.Pattern[y];

                for (int x = 0; x < recipeW; x++)
                {
                    char c = x < row.Length ? row[x] : ' ';
                    ResourceId gridItem = grid.GetSlot(gridMinX + x, gridMinY + y);

                    if (c == ' ')
                    {
                        // Expect empty
                        if (gridItem.Namespace != null)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // Expect specific item
                        if (!recipe.Keys.TryGetValue(c, out ResourceId expected))
                        {
                            return false;
                        }

                        if (gridItem.Namespace == null || gridItem != expected)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool MatchesShapeless(CraftingGrid grid, RecipeDefinition recipe)
        {
            // Collect all non-empty items from grid
            List<ResourceId> gridItems = new List<ResourceId>();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    ResourceId item = grid.GetSlot(x, y);

                    if (item.Namespace != null)
                    {
                        gridItems.Add(item);
                    }
                }
            }

            // Must have same count
            if (gridItems.Count != recipe.Ingredients.Count)
            {
                return false;
            }

            // Mark matched ingredients
            bool[] matched = new bool[recipe.Ingredients.Count];

            for (int g = 0; g < gridItems.Count; g++)
            {
                bool found = false;

                for (int r = 0; r < recipe.Ingredients.Count; r++)
                {
                    if (!matched[r] && gridItems[g] == recipe.Ingredients[r])
                    {
                        matched[r] = true;
                        found = true;

                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Total number of registered recipes.
        /// </summary>
        public int RecipeCount
        {
            get { return _recipes.Count; }
        }
    }
}
