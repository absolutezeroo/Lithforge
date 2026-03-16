using Lithforge.Runtime.BlockEntity.Behaviors;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Part Builder block entity (TiC-faithful).
    /// 3 slots: Material (0) + Pattern (1) + Output (2).
    /// Pattern slot accepts only items tagged "pattern".
    /// Without a pattern in the slot, no pattern buttons are shown.
    /// Each craft consumes 1 pattern item (unless tagged "pattern_reusable")
    /// plus N material items.
    /// </summary>
    public sealed class PartBuilderBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:part_builder";
        public const int TotalSlotCount = 3;
        public const int MaterialSlot = 0;
        public const int PatternSlot = 1;
        public const int OutputSlot = 2;

        private readonly InventoryBehavior _inventory;

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory
        {
            get { return _inventory; }
        }

        public PartBuilderBlockEntity()
        {
            _inventory = new InventoryBehavior(TotalSlotCount);
            Behaviors = new BlockEntityBehavior[] { _inventory };
        }
    }
}
