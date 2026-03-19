using Lithforge.Item;
using Lithforge.Runtime.Content.Tools;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Loads ToolTraitDefinition ScriptableObjects and builds the ToolTraitRegistry.</summary>
    public sealed class LoadToolTraitsPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading tool traits...";
            }
        }

        /// <summary>Loads tool trait assets, converts them to Tier 2 data, and registers them.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            ToolTraitDefinition[] toolTraits =
                Resources.LoadAll<ToolTraitDefinition>("Content/ToolTraits");
            ToolTraitRegistry toolTraitRegistry = new();

            for (int i = 0; i < toolTraits.Length; i++)
            {
                ToolTraitDefinition traitDef = toolTraits[i];

                if (string.IsNullOrEmpty(traitDef.traitId))
                {
                    continue;
                }

                ToolTraitData traitData = traitDef.ToTier2();
                toolTraitRegistry.Register(traitData);
            }

            ctx.ToolTraitRegistry = toolTraitRegistry;
            ctx.Logger.LogInfo($"Loaded {toolTraitRegistry.Count} tool traits.");
        }
    }
}
