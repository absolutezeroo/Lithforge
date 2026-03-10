using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Resolved crafting recipe entry for the CraftingEngine.
    /// Built from ScriptableObject data by the ContentPipeline.
    /// Tier 2 type — no UnityEngine dependencies.
    /// </summary>
    public sealed class RecipeEntry
    {
        public ResourceId Id { get; }

        public RecipeType Type { get; set; }

        public ResourceId ResultItem { get; set; }

        public int ResultCount { get; set; }

        /// <summary>
        /// Shaped pattern rows (e.g. ["## ", "## ", "   "]).
        /// Each character maps to a key in the Keys dictionary.
        /// Space means empty slot.
        /// </summary>
        public List<string> Pattern { get; set; }

        /// <summary>
        /// Key mapping for shaped recipes. Maps single character to item ResourceId.
        /// </summary>
        public Dictionary<char, ResourceId> Keys { get; set; }

        /// <summary>
        /// Ingredients for shapeless recipes. Each entry is an item ResourceId.
        /// </summary>
        public List<ResourceId> Ingredients { get; set; }

        public RecipeEntry(ResourceId id)
        {
            Id = id;
            Type = RecipeType.Shaped;
            ResultCount = 1;
            Pattern = new List<string>();
            Keys = new Dictionary<char, ResourceId>();
            Ingredients = new List<ResourceId>();
        }
    }
}
