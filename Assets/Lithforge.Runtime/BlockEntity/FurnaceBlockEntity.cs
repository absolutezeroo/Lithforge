using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Item.Crafting;
using Lithforge.Item;
using Lithforge.Voxel.Crafting;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Furnace block entity: 3-slot inventory (input=0, fuel=1, output=2)
    ///     with fuel burn and smelting behaviors.
    /// </summary>
    public sealed class FurnaceBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:furnace";

        public const int FurnaceSlotCount = 3;

        public const int InputSlot = 0;

        public const int FuelSlot = 1;

        public const int OutputSlot = 2;

        public FurnaceBlockEntity(SmeltingRecipeRegistry recipeRegistry, ItemRegistry itemRegistry)
        {
            Inventory = new InventoryBehavior(FurnaceSlotCount);
            FuelBurn = new FuelBurnBehavior(Inventory, itemRegistry, FuelSlot);
            Smelting = new SmeltingBehavior(Inventory, FuelBurn, recipeRegistry, itemRegistry);
            Behaviors = new BlockEntityBehavior[]
            {
                Inventory,
                FuelBurn,
                Smelting,
            };
        }

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory { get; }

        public FuelBurnBehavior FuelBurn { get; }

        public SmeltingBehavior Smelting { get; }
    }
}
