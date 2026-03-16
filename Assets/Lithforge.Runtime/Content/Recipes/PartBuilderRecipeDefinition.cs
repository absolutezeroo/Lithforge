using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// Data-driven recipe for the Part Builder.
    /// Each asset represents one pattern button in the Part Builder UI.
    /// The material is resolved dynamically from the input slot.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPartBuilderRecipe",
        menuName = "Lithforge/Content/Part Builder Recipe")]
    public sealed class PartBuilderRecipeDefinition : ScriptableObject
    {
        /// <summary>The part type this recipe produces (determines the pattern icon).</summary>
        [Header("Pattern")]
        [Tooltip("The part type this recipe produces (determines the pattern icon)")]
        public ToolPartType resultPartType;

        /// <summary>Display name for the pattern button in the UI.</summary>
        [Tooltip("Display name for the pattern button in the UI")]
        public string displayName;

        /// <summary>Material cost in items. If 0, uses the material's default partBuilderCost.</summary>
        [Header("Cost")]
        [Tooltip("Material cost in items. If 0, uses the material's default partBuilderCost.")]
        [Min(0)] public int costOverride;

        /// <summary>Item ID of the result generic part (e.g. lithforge:tool_head).</summary>
        [Header("Result")]
        [Tooltip("Item ID of the result generic part (e.g. lithforge:tool_head)")]
        public string resultItemId;

        /// <summary>Number of parts produced per craft.</summary>
        [Tooltip("Number of parts produced per craft")]
        [Min(1)] public int resultCount = 1;

        /// <summary>Tag that the pattern slot item must have. Default: "pattern".</summary>
        [Header("Pattern Item")]
        [Tooltip("Tag that the pattern slot item must have. Default: 'pattern' (blank pattern). " +
                 "Leave empty to use default.")]
        public string requiredPatternTag = "pattern";
    }
}
