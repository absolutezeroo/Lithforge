using System;
using System.IO;

using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Voxel.Crafting;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    ///     Tracks smelting progress for furnace-like block entities.
    ///     Reads input from slot 0, writes output to slot 2 (furnace layout: input=0, fuel=1, output=2).
    ///     Advances progress only when FuelBurnBehavior.IsFueled is true and a valid recipe exists.
    /// </summary>
    public sealed class SmeltingBehavior : BlockEntityBehavior
    {
        /// <summary>Optional callback invoked when smelt progress advances. Wired by BlockEntity.SetHost.</summary>
        private Action _onChanged;

        /// <summary>Duration in seconds to complete one smelting operation.</summary>
        private const float SmeltDuration = 10f;

        /// <summary>Inventory slot index for the smelting input.</summary>
        private const int InputSlotIndex = 0;

        /// <summary>Inventory slot index for the smelting output.</summary>
        private const int OutputSlotIndex = 2;

        /// <summary>Sentinel value for versioned serialization format.</summary>
        private const int VersionSentinel = int.MinValue + 11;

        /// <summary>Reference to the fuel burn behavior for fuel availability checks.</summary>
        private readonly FuelBurnBehavior _fuelBurn;

        /// <summary>Reference to the parent inventory for slot access.</summary>
        private readonly InventoryBehavior _inventory;

        /// <summary>Item registry for looking up result item max stack sizes.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Registry of smelting recipes for input-to-output matching.</summary>
        private readonly SmeltingRecipeRegistry _recipeRegistry;

        /// <summary>Accumulated smelting progress in seconds toward completing the current item.</summary>
        private float _smeltProgress;

        /// <summary>Creates a smelting behavior with inventory, fuel, recipe, and item registry references.</summary>
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

        /// <summary>Injects the change notification callback. Called by BlockEntity.SetHost.</summary>
        public override void SetOnChanged(Action onChanged)
        {
            _onChanged = onChanged;
        }

        /// <summary>
        ///     Progress of current smelt operation (0 to 1).
        /// </summary>
        public float SmeltProgress
        {
            get { return _smeltProgress / SmeltDuration; }
        }

        /// <summary>Advances smelting progress if fuel and a valid recipe are available.</summary>
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
            int maxStack = resultItem?.MaxStackSize ?? 64;

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
            _onChanged?.Invoke();

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

        /// <summary>Serializes the current smelt progress to the writer.</summary>
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(VersionSentinel);
            writer.Write(_smeltProgress);
        }

        /// <summary>Deserializes smelt progress, auto-detecting versioned vs legacy format.</summary>
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

        /// <summary>Reinterprets raw int bytes as a float for legacy format migration.</summary>
        private static float ReinterpretIntAsFloat(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
