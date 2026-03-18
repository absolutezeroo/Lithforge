using Lithforge.Runtime.BlockEntity.Behaviors;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Chest block entity: 27-slot inventory storage.
    /// </summary>
    public sealed class ChestBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:chest";

        public const int ChestSlotCount = 27;

        public ChestBlockEntity()
        {
            Inventory = new InventoryBehavior(ChestSlotCount);
            Behaviors = new BlockEntityBehavior[]
            {
                Inventory,
            };
        }

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory { get; }
    }
}
