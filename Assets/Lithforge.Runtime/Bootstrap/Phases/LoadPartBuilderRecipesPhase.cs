using Lithforge.Core.Data;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.Content.Recipes;
using Lithforge.Voxel.Crafting;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Loads PartBuilderRecipeDefinition ScriptableObjects and builds the PartBuilderRecipeRegistry.</summary>
    public sealed class LoadPartBuilderRecipesPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading part builder recipes...";
            }
        }

        /// <summary>Loads part builder recipe assets and registers them into the PartBuilderRecipeRegistry.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            PartBuilderRecipeDefinition[] pbRecipeDefs =
                Resources.LoadAll<PartBuilderRecipeDefinition>("Content/Recipes/PartBuilder");
            PartBuilderRecipeRegistry partBuilderRecipeRegistry = new();

            for (int i = 0; i < pbRecipeDefs.Length; i++)
            {
                PartBuilderRecipeDefinition def = pbRecipeDefs[i];
                int cost = def.costOverride > 0 ? def.costOverride : 0;
                string tag = string.IsNullOrEmpty(def.requiredPatternTag)
                    ? "pattern"
                    : def.requiredPatternTag;

                partBuilderRecipeRegistry.Register(new PartBuilderRecipe(
                    def.resultPartType, def.displayName, cost,
                    ResourceId.Parse(def.resultItemId), def.resultCount, tag));
            }

            ctx.PartBuilderRecipeRegistry = partBuilderRecipeRegistry;
            ctx.Logger.LogInfo($"Loaded {partBuilderRecipeRegistry.Count} part builder recipes.");
        }
    }
}
