using Lithforge.Core.Data;
using Lithforge.Voxel.Crafting;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Repair calculation helper, shared between inventory repair and station repair.
    /// TiC formula: repairAmount = headDurability * repairKitValue / UNITS_PER_REPAIR.
    /// </summary>
    public static class RepairKitHelper
    {
        /// <summary>
        /// Units of material per full headDurability repair.
        /// TiC uses INGOTS_PER_REPAIR = 3.0f.
        /// </summary>
        public const float UnitsPerRepair = 3.0f;

        /// <summary>
        /// Repair kit value in material units.
        /// TiC default repairKitAmount = 2.0.
        /// A kit costs 2 units to craft and is worth 2 units of repair.
        /// So 1 kit restores headDurability * 2.0 / 3.0 = 66% of head durability.
        /// </summary>
        public const float RepairKitValue = 2.0f;

        /// <summary>
        /// Calculates durability restored when using a repair kit on a tool.
        /// Returns 0 if material doesn't match or tool is at full durability.
        /// </summary>
        public static int CalculateRepairKitRepair(
            ToolInstance tool,
            ToolPartData kitData,
            ToolMaterialRegistry materialRegistry)
        {
            if (tool == null || kitData.PartType != ToolPartType.RepairKit)
            {
                return 0;
            }

            ResourceId headMaterial = GetHeadMaterial(tool);

            if (headMaterial.Namespace == null)
            {
                return 0;
            }

            if (!kitData.MaterialId.Equals(headMaterial))
            {
                return 0;
            }

            int damage = tool.IsBroken
                ? tool.MaxDurability
                : tool.MaxDurability - tool.CurrentDurability;

            if (damage <= 0)
            {
                return 0;
            }

            ToolMaterialData matData = materialRegistry.Get(headMaterial);

            if (matData == null)
            {
                return 0;
            }

            int repair = (int)(matData.HeadDurability * RepairKitValue / UnitsPerRepair);
            return System.Math.Min(repair, damage);
        }

        /// <summary>
        /// Calculates durability restored per raw material item.
        /// Used by Tool Station repair with raw materials.
        /// </summary>
        public static int CalculateRawMaterialRepair(
            ToolInstance tool,
            MaterialInputData inputData,
            ToolMaterialRegistry materialRegistry)
        {
            if (tool == null || inputData == null)
            {
                return 0;
            }

            ResourceId headMaterial = GetHeadMaterial(tool);

            if (headMaterial.Namespace == null)
            {
                return 0;
            }

            if (!inputData.MaterialId.Equals(headMaterial))
            {
                return 0;
            }

            ToolMaterialData matData = materialRegistry.Get(headMaterial);

            if (matData == null)
            {
                return 0;
            }

            float valuePerItem = inputData.Value / (float)inputData.Needed;
            return (int)(matData.HeadDurability * valuePerItem / UnitsPerRepair);
        }

        /// <summary>
        /// Finds the head material of a tool by scanning parts for head-class types.
        /// </summary>
        public static ResourceId GetHeadMaterial(ToolInstance tool)
        {
            if (tool.Parts == null)
            {
                return default;
            }

            for (int i = 0; i < tool.Parts.Length; i++)
            {
                ToolPartType pt = tool.Parts[i].PartType;

                if (pt == ToolPartType.Head || pt == ToolPartType.Blade || pt == ToolPartType.Point)
                {
                    return tool.Parts[i].MaterialId;
                }
            }

            return default;
        }
    }
}
