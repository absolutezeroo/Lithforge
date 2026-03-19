using Lithforge.Runtime.BlockEntity.Behaviors;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Chest block entity: 27-slot inventory storage.
    /// </summary>
    public sealed class ChestBlockEntity : BlockEntity
    {
        /// <summary>Unique type identifier for chest block entities.</summary>
        public const string TypeIdValue = "lithforge:chest";

        /// <summary>Number of inventory slots in a chest.</summary>
        public const int ChestSlotCount = 27;

        /// <summary>Creates a chest block entity with a 27-slot inventory.</summary>
        public ChestBlockEntity()
        {
            Inventory = new InventoryBehavior(ChestSlotCount);
            Behaviors = new BlockEntityBehavior[]
            {
                Inventory,
            };
        }

        /// <summary>Returns the chest type identifier.</summary>
        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        /// <summary>The chest's inventory behavior managing 27 item slots.</summary>
        public InventoryBehavior Inventory { get; }
    }
}
