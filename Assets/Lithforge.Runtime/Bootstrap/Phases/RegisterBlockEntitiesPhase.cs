using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.Factories;
using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 16: Registers all block entity types (chest, furnace, tool station, crafting table, part builder).</summary>
    public sealed class RegisterBlockEntitiesPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Registering block entities...";
            }
        }

        /// <summary>Creates the BlockEntityRegistry and registers factory instances for each block entity type.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            BlockEntityRegistry blockEntityRegistry = new();
            blockEntityRegistry.Register(new BlockEntityType(
                ChestBlockEntity.TypeIdValue,
                new ChestBlockEntityFactory()));
            blockEntityRegistry.Register(new BlockEntityType(
                FurnaceBlockEntity.TypeIdValue,
                new FurnaceBlockEntityFactory(ctx.SmeltingRecipeRegistry, ctx.ItemRegistry)));
            blockEntityRegistry.Register(new BlockEntityType(
                ToolStationBlockEntity.TypeIdValue,
                new ToolStationBlockEntityFactory(ctx.ToolMaterialRegistry, ctx.ItemRegistry)));
            blockEntityRegistry.Register(new BlockEntityType(
                CraftingTableBlockEntity.TypeIdValue,
                new CraftingTableBlockEntityFactory()));
            blockEntityRegistry.Register(new BlockEntityType(
                PartBuilderBlockEntity.TypeIdValue,
                new PartBuilderBlockEntityFactory()));
            blockEntityRegistry.Freeze();

            ctx.BlockEntityRegistry = blockEntityRegistry;
            ctx.Logger.LogInfo($"Registered {blockEntityRegistry.Count} block entity types.");
        }
    }
}
