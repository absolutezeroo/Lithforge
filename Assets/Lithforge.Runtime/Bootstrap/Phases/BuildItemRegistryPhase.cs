using Lithforge.Item;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class BuildItemRegistryPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Building item registry...";
            }
        }

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
