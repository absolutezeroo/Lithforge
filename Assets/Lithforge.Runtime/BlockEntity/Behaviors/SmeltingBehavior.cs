using System.IO;
using Lithforge.Item.Crafting;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    /// Tracks smelting progress for furnace-like block entities.
    /// Reads input from slot 0, writes output to slot 2 (furnace layout: input=0, fuel=1, output=2).
    /// Advances progress only when FuelBurnBehavior.IsFueled is true and a valid recipe exists.
    /// </summary>
    public sealed class SmeltingBehavior : BlockEntityBehavior
    {
        private const float SmeltDuration = 10f; // seconds to smelt one item
        private const int InputSlotIndex = 0;
        private const int OutputSlotIndex = 2;

        private readonly InventoryBehavior _inventory;
        private readonly FuelBurnBehavior _fuelBurn;
        private readonly SmeltingRecipeRegistry _recipeRegistry;
        private readonly ItemRegistry _itemRegistry;

        private float _smeltProgress;

        /// <summary>
        /// Progress of current smelt operation (0 to 1).
        /// </summary>
        public float SmeltProgress
        {
            get { return _smeltProgress / SmeltDuration; }
        }

        public SmeltingBehavior(
            InventoryBehavior inventory,
            FuelBurnBehavior fuelBurn,
            SmeltingRecipeRegistry recipeRegistry,
            ItemRegistry itemRegistry)
        {
            _inventory = inventory;
            _fuelBurn = fuelBurn;
            _recipeRegistry = recipeRegistry;
            _itemRegistry = itemRegistry;
        }

        public override void Tick(float deltaTime)
        {
            ItemStack inputSlot = _inventory.GetSlot(InputSlotIndex);

            if (inputSlot.IsEmpty)
            {
                _smeltProgress = 0f;

                return;
            }

            SmeltingRecipeEntry recipe = _recipeRegistry.FindMatch(inputSlot.ItemId);

            if (recipe == null)
            {
                _smeltProgress = 0f;

                return;
            }

            // Check if output slot can accept the result
            ItemStack outputSlot = _inventory.GetSlot(OutputSlotIndex);
            ItemEntry resultItem = _itemRegistry.Get(recipe.ResultItem);
            int maxStack = resultItem != null ? resultItem.MaxStackSize : 64;

            if (!outputSlot.IsEmpty)
            {
                if (outputSlot.ItemId != recipe.ResultItem ||
                    outputSlot.Count + recipe.ResultCount > maxStack)
                {
                    // Output full or wrong item type — stall
                    return;
                }
            }

            // Try to get fuel
            if (!_fuelBurn.TryConsumeFuel())
            {
                // No fuel — stall but don't reset progress
                return;
            }

            _smeltProgress += deltaTime;

            if (_smeltProgress >= SmeltDuration)
            {
                // Smelt complete — consume input, produce output
                _smeltProgress = 0f;

                ItemStack updatedInput = inputSlot;
                updatedInput.Count -= 1;

                if (updatedInput.Count <= 0)
                {
                    _inventory.SetSlot(InputSlotIndex, ItemStack.Empty);
                }
                else
                {
                    _inventory.SetSlot(InputSlotIndex, updatedInput);
                }

                if (outputSlot.IsEmpty)
                {
                    _inventory.SetSlot(OutputSlotIndex,
                        new ItemStack(recipe.ResultItem, recipe.ResultCount));
                }
                else
                {
                    ItemStack updatedOutput = outputSlot;
                    updatedOutput.Count += recipe.ResultCount;
                    _inventory.SetSlot(OutputSlotIndex, updatedOutput);
                }
            }
        }

        private const int VersionSentinel = int.MinValue + 11;

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(VersionSentinel);
            writer.Write(_smeltProgress);
        }

        public override void Deserialize(BinaryReader reader)
        {
            int firstInt = reader.ReadInt32();

            if (firstInt == VersionSentinel)
            {
                _smeltProgress = reader.ReadSingle();
            }
            else
            {
                // Legacy format: firstInt is the IEEE-754 bytes of _smeltProgress
                _smeltProgress = ReinterpretIntAsFloat(firstInt);
            }
        }

        private static float ReinterpretIntAsFloat(int value)
        {
            byte[] bytes = System.BitConverter.GetBytes(value);
            return System.BitConverter.ToSingle(bytes, 0);
        }
    }
}
