using System;
using System.IO;

using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    ///     Tracks fuel burn state for furnace-like block entities.
    ///     Reads the fuel slot (slot 1) from InventoryBehavior and consumes fuel items
    ///     when burn time runs out and smelting is possible.
    /// </summary>
    public sealed class FuelBurnBehavior : BlockEntityBehavior
    {
        private const int VersionSentinel = int.MinValue + 10;

        private readonly int _fuelSlotIndex;
        private readonly InventoryBehavior _inventory;

        private readonly ItemRegistry _itemRegistry;

        private float _burnTimeRemaining;

        private float _maxBurnTime;

        public FuelBurnBehavior(InventoryBehavior inventory, ItemRegistry itemRegistry, int fuelSlotIndex)
        {
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _fuelSlotIndex = fuelSlotIndex;
        }

        /// <summary>
        ///     True if fuel is currently burning (burn time remaining > 0).
        /// </summary>
        public bool IsFueled
        {
            get { return _burnTimeRemaining > 0f; }
        }

        /// <summary>
        ///     Progress of current fuel burn (0 = just started, 1 = fully consumed).
        ///     Returns 0 if not burning.
        /// </summary>
        public float BurnProgress
        {
            get
            {
                if (_maxBurnTime <= 0f)
                {
                    return 0f;
                }

                return 1f - _burnTimeRemaining / _maxBurnTime;
            }
        }

        public override void Tick(float deltaTime)
        {
            if (_burnTimeRemaining > 0f)
            {
                _burnTimeRemaining -= deltaTime;

                if (_burnTimeRemaining < 0f)
                {
                    _burnTimeRemaining = 0f;
                }
            }
        }

        /// <summary>
        ///     Attempts to consume one fuel item if not currently burning.
        ///     Called by SmeltingBehavior when it needs fuel to continue.
        ///     Returns true if fuel was consumed.
        /// </summary>
        public bool TryConsumeFuel()
        {
            if (_burnTimeRemaining > 0f)
            {
                return true;
            }

            ItemStack fuelSlot = _inventory.GetSlot(_fuelSlotIndex);

            if (fuelSlot.IsEmpty)
            {
                return false;
            }

            ItemEntry fuelItem = _itemRegistry.Get(fuelSlot.ItemId);

            if (fuelItem == null || fuelItem.FuelTime <= 0f)
            {
                return false;
            }

            _maxBurnTime = fuelItem.FuelTime;
            _burnTimeRemaining = _maxBurnTime;

            // Consume one fuel item
            ItemStack updated = fuelSlot;
            updated.Count -= 1;

            if (updated.Count <= 0)
            {
                _inventory.SetSlot(_fuelSlotIndex, ItemStack.Empty);
            }
            else
            {
                _inventory.SetSlot(_fuelSlotIndex, updated);
            }

            return true;
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(VersionSentinel);
            writer.Write(_burnTimeRemaining);
            writer.Write(_maxBurnTime);
        }

        public override void Deserialize(BinaryReader reader)
        {
            int firstInt = reader.ReadInt32();

            if (firstInt == VersionSentinel)
            {
                _burnTimeRemaining = reader.ReadSingle();
                _maxBurnTime = reader.ReadSingle();
            }
            else
            {
                // Legacy format: firstInt is the IEEE-754 bytes of _burnTimeRemaining
                _burnTimeRemaining = ReinterpretIntAsFloat(firstInt);
                _maxBurnTime = reader.ReadSingle();
            }
        }

        private static float ReinterpretIntAsFloat(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
