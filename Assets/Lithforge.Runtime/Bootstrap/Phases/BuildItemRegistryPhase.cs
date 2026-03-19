using Lithforge.Item;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 12: Registers block items and standalone items into the unified ItemRegistry.</summary>
    public sealed class BuildItemRegistryPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Building item registry...";
            }
        }

        /// <summary>Creates the ItemRegistry and registers all block items and standalone item entries.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            ItemRegistry itemRegistry = new();
            itemRegistry.RegisterBlockItems(ctx.StateRegistry.Entries);
            itemRegistry.RegisterItems(ctx.ItemEntries);
            ctx.ItemRegistry = itemRegistry;
            ctx.Logger.LogInfo($"ItemRegistry: {itemRegistry.Count} items total.");
        }
    }
}
