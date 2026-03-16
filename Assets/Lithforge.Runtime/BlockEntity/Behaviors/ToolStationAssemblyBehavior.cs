using Lithforge.Core.Data;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    /// Behavior that watches part slots and assembles a ToolInstance
    /// when a valid combination of parts is placed.
    /// No tick needed — assembly is computed on demand when the UI queries.
    /// </summary>
    public sealed class ToolStationAssemblyBehavior : BlockEntityBehavior
    {
        private readonly InventoryBehavior _inventory;
        private readonly ToolMaterialRegistry _materialRegistry;
        private readonly ItemRegistry _itemRegistry;

        public ToolStationAssemblyBehavior(
            InventoryBehavior inventory,
            ToolMaterialRegistry materialRegistry,
            ItemRegistry itemRegistry)
        {
            _inventory = inventory;
            _materialRegistry = materialRegistry;
            _itemRegistry = itemRegistry;
        }

        /// <summary>
        /// Attempts to assemble a tool from the current part slots.
        /// Returns null if the parts are invalid or incomplete.
        /// </summary>
        public ToolInstance TryAssemble(ToolType selectedToolType)
        {
            if (selectedToolType == ToolType.None)
            {
                return null;
            }

            // Collect parts from slots 0-2
            int partCount = 0;
            ToolPart[] parts = new ToolPart[ToolStationBlockEntity.PartSlotCount];

            for (int i = 0; i < ToolStationBlockEntity.PartSlotCount; i++)
            {
                int slotIndex = ToolStationBlockEntity.HeadSlot + i;
                ItemStack stack = _inventory.GetSlot(slotIndex);

                if (stack.IsEmpty)
                {
                    continue;
                }

                ItemEntry itemDef = _itemRegistry.Get(stack.ItemId);

                if (itemDef == null)
                {
                    continue;
                }

                // Resolve part type and material from CustomData (generic parts)
                // or fall back to tag-based resolution (legacy items)
                ToolPartType partType = ResolvePartType(itemDef, i, stack);

                if (partType == ToolPartType.None)
                {
                    continue;
                }

                ToolPart part = ToolPart.Empty;
                part.PartType = partType;
                part.MaterialId = ResolveMaterialId(itemDef, stack);
                parts[partCount] = part;
                partCount++;
            }

            if (partCount == 0)
            {
                return null;
            }

            // Trim to actual size
            ToolPart[] trimmed = new ToolPart[partCount];
            System.Array.Copy(parts, trimmed, partCount);

            return ToolAssembler.Assemble(selectedToolType, trimmed, _materialRegistry);
        }

        /// <summary>
        /// Consumes the input parts (slots 0-2) by one each.
        /// Call after the player takes the output.
        /// </summary>
        public void ConsumeInputParts()
        {
            for (int i = 0; i < ToolStationBlockEntity.PartSlotCount; i++)
            {
                int slotIndex = ToolStationBlockEntity.HeadSlot + i;
                ItemStack stack = _inventory.GetSlot(slotIndex);

                if (!stack.IsEmpty)
                {
                    ItemStack updated = stack;
                    updated.Count -= 1;

                    if (updated.Count <= 0)
                    {
                        _inventory.SetSlot(slotIndex, ItemStack.Empty);
                    }
                    else
                    {
                        _inventory.SetSlot(slotIndex, updated);
                    }
                }
            }
        }

        private static ToolPartType ResolvePartType(ItemEntry itemDef, int slotPosition, ItemStack stack)
        {
            // New path: generic parts with CustomData
            if (stack.HasCustomData &&
                ToolPartDataSerializer.TryDeserialize(stack.CustomData, out ToolPartData partData))
            {
                return partData.PartType;
            }

            // Legacy fallback: tag-based resolution
            if (itemDef.Tags != null)
            {
                for (int t = 0; t < itemDef.Tags.Count; t++)
                {
                    string tag = itemDef.Tags[t];

                    if (tag == "tool_part_head")
                    {
                        return ToolPartType.Head;
                    }

                    if (tag == "tool_part_blade")
                    {
                        return ToolPartType.Blade;
                    }

                    if (tag == "tool_part_handle")
                    {
                        return ToolPartType.Handle;
                    }

                    if (tag == "tool_part_binding")
                    {
                        return ToolPartType.Binding;
                    }

                    if (tag == "tool_part_guard")
                    {
                        return ToolPartType.Guard;
                    }
                }
            }

            // Fallback: infer from slot position
            switch (slotPosition)
            {
                case 0: return ToolPartType.Head;
                case 1: return ToolPartType.Handle;
                case 2: return ToolPartType.Binding;
                default: return ToolPartType.None;
            }
        }

        private static ResourceId ResolveMaterialId(ItemEntry itemDef, ItemStack stack)
        {
            // New path: generic parts with CustomData
            if (stack.HasCustomData &&
                ToolPartDataSerializer.TryDeserialize(stack.CustomData, out ToolPartData partData))
            {
                return partData.MaterialId;
            }

            // Legacy fallback: tag-based convention "material:lithforge:iron"
            if (itemDef.Tags != null)
            {
                for (int t = 0; t < itemDef.Tags.Count; t++)
                {
                    string tag = itemDef.Tags[t];

                    if (tag.StartsWith("material:"))
                    {
                        string matStr = tag.Substring("material:".Length);

                        if (ResourceId.TryParse(matStr, out ResourceId matId))
                        {
                            return matId;
                        }
                    }
                }
            }

            // Fallback: use the item's own ID as material reference
            return itemDef.Id;
        }
    }
}
