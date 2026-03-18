using Lithforge.Voxel.Block;

namespace Lithforge.Item
{
    /// <summary>
    ///     Mutable context passed through the mining modifier pipeline.
    ///     Populated from block and tool data, then modified by affixes/enchantments.
    /// </summary>
    public struct MiningContext
    {
        public float Hardness;

        public BlockMaterialType Material;

        public int RequiredToolLevel;

        public ToolType ToolType;

        public int ToolLevel;

        public float ToolSpeed;

        public bool IsCorrectTool;

        public bool CanHarvest;

        public float SpeedMultiplier;

        public float FlatSpeedBonus;

        public float HardnessReduction;

        public static MiningContext Default
        {
            get
            {
                return new MiningContext
                {
                    SpeedMultiplier = 1.0f, CanHarvest = true,
                };
            }
        }
    }
}
