using Lithforge.Runtime.BlockEntity.Behaviors;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Chest block entity: 27-slot inventory storage.
    /// </summary>
    public sealed class ChestBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:chest";
        public const int ChestSlotCount = 27;

        private readonly InventoryBehavior _inventory;

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory
        {
            get { return _inventory; }
        }

        public ChestBlockEntity()
        {
            _inventory = new InventoryBehavior(ChestSlotCount);
            Behaviors = new BlockEntityBehavior[] { _inventory };
        }
    }
}
