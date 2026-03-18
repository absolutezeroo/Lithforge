using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.Factories;
using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class RegisterBlockEntitiesPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Registering block entities...";
            }
        }

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
