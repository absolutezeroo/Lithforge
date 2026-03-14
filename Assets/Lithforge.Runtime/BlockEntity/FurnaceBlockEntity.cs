using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Furnace block entity: 3-slot inventory (input=0, fuel=1, output=2)
    /// with fuel burn and smelting behaviors.
    /// </summary>
    public sealed class FurnaceBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:furnace";
        public const int FurnaceSlotCount = 3;
        public const int InputSlot = 0;
        public const int FuelSlot = 1;
        public const int OutputSlot = 2;

        private readonly InventoryBehavior _inventory;
        private readonly FuelBurnBehavior _fuelBurn;
        private readonly SmeltingBehavior _smelting;

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory
        {
            get { return _inventory; }
        }

        public FuelBurnBehavior FuelBurn
        {
            get { return _fuelBurn; }
        }

        public SmeltingBehavior Smelting
        {
            get { return _smelting; }
        }

        public FurnaceBlockEntity(SmeltingRecipeRegistry recipeRegistry, ItemRegistry itemRegistry)
        {
            _inventory = new InventoryBehavior(FurnaceSlotCount);
            _fuelBurn = new FuelBurnBehavior(_inventory, itemRegistry, FuelSlot);
            _smelting = new SmeltingBehavior(_inventory, _fuelBurn, recipeRegistry, itemRegistry);
            Behaviors = new BlockEntityBehavior[] { _inventory, _fuelBurn, _smelting };
        }
    }
}
