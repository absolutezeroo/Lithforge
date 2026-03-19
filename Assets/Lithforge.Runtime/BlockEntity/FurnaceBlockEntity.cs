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
        /// <summary>Unique type identifier for furnace block entities.</summary>
        public const string TypeIdValue = "lithforge:furnace";

        /// <summary>Total number of inventory slots in a furnace.</summary>
        public const int FurnaceSlotCount = 3;

        /// <summary>Index of the input (ore/raw item) slot.</summary>
        public const int InputSlot = 0;

        /// <summary>Index of the fuel slot.</summary>
        public const int FuelSlot = 1;

        /// <summary>Index of the output (smelted result) slot.</summary>
        public const int OutputSlot = 2;

        /// <summary>Creates a furnace with inventory, fuel burn, and smelting behaviors.</summary>
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

        /// <summary>Returns the furnace type identifier.</summary>
        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        /// <summary>The furnace's 3-slot inventory behavior (input, fuel, output).</summary>
        public InventoryBehavior Inventory { get; }

        /// <summary>The fuel burn behavior managing fuel consumption and burn time.</summary>
        public FuelBurnBehavior FuelBurn { get; }

        /// <summary>The smelting behavior managing recipe matching and cook progress.</summary>
        public SmeltingBehavior Smelting { get; }
    }
}
