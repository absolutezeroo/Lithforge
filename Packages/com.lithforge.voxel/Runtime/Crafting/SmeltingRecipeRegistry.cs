using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// O(1) lookup registry for smelting recipes, keyed by input item.
    /// Built by ContentPipeline from SmeltingRecipeDefinition assets.
    /// </summary>
    public sealed class SmeltingRecipeRegistry
    {
        private readonly Dictionary<ResourceId, SmeltingRecipeEntry> _recipes = new();

        public void Register(SmeltingRecipeEntry entry)
        {
            _recipes[entry.InputItem] = entry;
        }

        /// <summary>
        /// Finds the smelting recipe for the given input item.
        /// Returns null if no recipe exists.
        /// </summary>
        public SmeltingRecipeEntry FindMatch(ResourceId inputItem)
        {
            _recipes.TryGetValue(inputItem, out SmeltingRecipeEntry entry);

            return entry;
        }

        public int Count
        {
            get { return _recipes.Count; }
        }
    }
}
