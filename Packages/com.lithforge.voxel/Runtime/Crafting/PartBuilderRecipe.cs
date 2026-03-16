using Lithforge.Core.Data;
using Lithforge.Voxel.Item;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Runtime recipe for the Part Builder.
    /// Each instance represents one pattern button in the Part Builder UI.
    /// The material is resolved dynamically from the input slot.
    /// </summary>
    public sealed class PartBuilderRecipe
    {
        public ToolPartType ResultPartType { get; }

        public string DisplayName { get; }

        public int Cost { get; }

        public ResourceId ResultItemId { get; }

        public int ResultCount { get; }

        public string RequiredPatternTag { get; }

        public PartBuilderRecipe(
            ToolPartType resultPartType,
            string displayName,
            int cost,
            ResourceId resultItemId,
            int resultCount,
            string requiredPatternTag)
        {
            ResultPartType = resultPartType;
            DisplayName = displayName;
            Cost = cost;
            ResultItemId = resultItemId;
            ResultCount = resultCount;
            RequiredPatternTag = requiredPatternTag;
        }
    }
}
