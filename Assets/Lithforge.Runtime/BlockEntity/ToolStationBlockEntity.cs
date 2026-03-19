using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Tool station block entity: Tinkers-style tool assembly station.
    ///     3 part slots (head, handle, binding) + output slot.
    ///     Assembly is computed on demand when the UI queries TryAssemble().
    /// </summary>
    public sealed class ToolStationBlockEntity : BlockEntity
    {
        /// <summary>Unique type identifier for tool station block entities.</summary>
        public const string TypeIdValue = "lithforge:tool_station";

        /// <summary>Total number of inventory slots (3 part + 1 output).</summary>
        public const int TotalSlotCount = 4;

        /// <summary>Number of part input slots (head, handle, binding).</summary>
        public const int PartSlotCount = 3;

        /// <summary>Index of the head part slot.</summary>
        public const int HeadSlot = 0;

        /// <summary>Index of the handle part slot.</summary>
        public const int HandleSlot = 1;

        /// <summary>Index of the binding part slot.</summary>
        public const int BindingSlot = 2;

        /// <summary>Index of the assembled tool output slot.</summary>
        public const int OutputSlot = 3;

        /// <summary>Creates a tool station with inventory and assembly behaviors.</summary>
        public ToolStationBlockEntity(
            ToolMaterialRegistry materialRegistry,
            ItemRegistry itemRegistry)
        {
            Inventory = new InventoryBehavior(TotalSlotCount);
            Assembly = new ToolStationAssemblyBehavior(Inventory, materialRegistry, itemRegistry);
            Behaviors = new BlockEntityBehavior[]
            {
                Inventory,
            };
        }

        /// <summary>Returns the tool station type identifier.</summary>
        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        /// <summary>The tool station's 4-slot inventory behavior.</summary>
        public InventoryBehavior Inventory { get; }

        /// <summary>Assembly behavior that computes tool output from placed parts.</summary>
        public ToolStationAssemblyBehavior Assembly { get; }
    }
}
