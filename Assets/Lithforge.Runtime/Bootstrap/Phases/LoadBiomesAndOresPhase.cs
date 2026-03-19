using Lithforge.Runtime.Content.WorldGen;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 7: Loads BiomeDefinition and OreDefinition ScriptableObjects from Resources.</summary>
    public sealed class LoadBiomesAndOresPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading biomes and ores...";
            }
        }

        /// <summary>Loads biome and ore definitions into the context.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            ctx.BiomeDefinitions = Resources.LoadAll<BiomeDefinition>("Content/Biomes");
            ctx.Logger.LogInfo($"Loaded {ctx.BiomeDefinitions.Length} biome definitions.");

            ctx.OreDefinitions = Resources.LoadAll<OreDefinition>("Content/Ores");
            ctx.Logger.LogInfo($"Loaded {ctx.OreDefinitions.Length} ore definitions.");
        }
    }
}
