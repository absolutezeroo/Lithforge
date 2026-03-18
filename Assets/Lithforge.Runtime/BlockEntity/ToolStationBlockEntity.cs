using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Tool station block entity: Tinkers-style tool assembly station.
    /// 3 part slots (head, handle, binding) + output slot.
    /// Assembly is computed on demand when the UI queries TryAssemble().
    /// </summary>
    public sealed class ToolStationBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:tool_station";
        public const int TotalSlotCount = 4;
        public const int PartSlotCount = 3;

        public const int HeadSlot = 0;
        public const int HandleSlot = 1;
        public const int BindingSlot = 2;
        public const int OutputSlot = 3;

        private readonly InventoryBehavior _inventory;
        private readonly ToolStationAssemblyBehavior _assembly;

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public InventoryBehavior Inventory
        {
            get { return _inventory; }
        }

        public ToolStationAssemblyBehavior Assembly
        {
            get { return _assembly; }
        }

        public ToolStationBlockEntity(
            ToolMaterialRegistry materialRegistry,
            ItemRegistry itemRegistry)
        {
            _inventory = new InventoryBehavior(TotalSlotCount);
            _assembly = new ToolStationAssemblyBehavior(_inventory, materialRegistry, itemRegistry);
            Behaviors = new BlockEntityBehavior[] { _inventory };
        }
    }
}
