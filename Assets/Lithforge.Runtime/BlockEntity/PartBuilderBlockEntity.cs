using Lithforge.Runtime.BlockEntity.Behaviors;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Part Builder block entity (TiC-faithful).
    ///     3 slots: Material (0) + Pattern (1) + Output (2).
    ///     Pattern slot accepts only items tagged "pattern".
    ///     Without a pattern in the slot, no pattern buttons are shown.
    ///     Each craft consumes 1 pattern item (unless tagged "pattern_reusable")
    ///     plus N material items.
    /// </summary>
    public sealed class PartBuilderBlockEntity : BlockEntity
    {
        /// <summary>Unique type identifier for part builder block entities.</summary>
        public const string TypeIdValue = "lithforge:part_builder";

        /// <summary>Total number of inventory slots (material, pattern, output).</summary>
        public const int TotalSlotCount = 3;

        /// <summary>Index of the material input slot.</summary>
        public const int MaterialSlot = 0;

        /// <summary>Index of the pattern input slot.</summary>
        public const int PatternSlot = 1;

        /// <summary>Index of the crafted output slot.</summary>
        public const int OutputSlot = 2;

        /// <summary>Creates a part builder entity with a 3-slot inventory.</summary>
        public PartBuilderBlockEntity()
        {
            Inventory = new InventoryBehavior(TotalSlotCount);
            Behaviors = new BlockEntityBehavior[]
            {
                Inventory,
            };
        }

        /// <summary>Returns the part builder type identifier.</summary>
        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        /// <summary>The part builder's 3-slot inventory behavior.</summary>
        public InventoryBehavior Inventory { get; }
    }
}
