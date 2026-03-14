using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// A single smelting recipe: one input item produces one output item.
    /// Used by furnace block entities to determine smelt results.
    /// </summary>
    public sealed class SmeltingRecipeEntry
    {
        public ResourceId InputItem { get; }

        public ResourceId ResultItem { get; }

        public int ResultCount { get; }

        public float ExperienceReward { get; }

        public SmeltingRecipeEntry(
            ResourceId inputItem,
            ResourceId resultItem,
            int resultCount,
            float experienceReward)
        {
            InputItem = inputItem;
            ResultItem = resultItem;
            ResultCount = resultCount;
            ExperienceReward = experienceReward;
        }
    }
}
