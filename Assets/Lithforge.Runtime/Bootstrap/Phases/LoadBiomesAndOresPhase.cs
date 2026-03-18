using Lithforge.Runtime.Content.WorldGen;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadBiomesAndOresPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading biomes and ores...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            ctx.BiomeDefinitions = Resources.LoadAll<BiomeDefinition>("Content/Biomes");
            ctx.Logger.LogInfo($"Loaded {ctx.BiomeDefinitions.Length} biome definitions.");

            ctx.OreDefinitions = Resources.LoadAll<OreDefinition>("Content/Ores");
            ctx.Logger.LogInfo($"Loaded {ctx.OreDefinitions.Length} ore definitions.");
        }
    }
}
