using Lithforge.Core.Data;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.Content.Recipes;
using Lithforge.Voxel.Crafting;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadSmeltingRecipesPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading smelting recipes...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            SmeltingRecipeRegistry smeltingRecipeRegistry = new();
            SmeltingRecipeDefinition[] smeltingRecipes =
                Resources.LoadAll<SmeltingRecipeDefinition>("Content/Recipes/Smelting");

            for (int i = 0; i < smeltingRecipes.Length; i++)
            {
                SmeltingRecipeDefinition sr = smeltingRecipes[i];

                if (!string.IsNullOrEmpty(sr.InputItemId) && !string.IsNullOrEmpty(sr.ResultItemId))
                {
                    ResourceId inputId = ResourceId.Parse(sr.InputItemId);
                    ResourceId resultId = ResourceId.Parse(sr.ResultItemId);
                    SmeltingRecipeEntry entry = new(
                        inputId, resultId, sr.ResultCount, sr.ExperienceReward);
                    smeltingRecipeRegistry.Register(entry);
                }
            }

            ctx.SmeltingRecipeRegistry = smeltingRecipeRegistry;
            ctx.Logger.LogInfo($"Loaded {smeltingRecipeRegistry.Count} smelting recipes.");
        }
    }
}
