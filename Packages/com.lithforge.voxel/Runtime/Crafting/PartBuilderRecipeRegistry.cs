using System.Collections.Generic;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Registry of Part Builder recipes.
    /// Each recipe maps to one pattern button in the Part Builder UI.
    /// </summary>
    public sealed class PartBuilderRecipeRegistry
    {
        private readonly List<PartBuilderRecipe> _recipes = new List<PartBuilderRecipe>();

        public void Register(PartBuilderRecipe recipe)
        {
            _recipes.Add(recipe);
        }

        public IReadOnlyList<PartBuilderRecipe> Recipes
        {
            get { return _recipes; }
        }

        public int Count
        {
            get { return _recipes.Count; }
        }
    }
}
