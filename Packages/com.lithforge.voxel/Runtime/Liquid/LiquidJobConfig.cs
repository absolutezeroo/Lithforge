using System.Runtime.InteropServices;

namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Blittable per-fluid configuration passed to <see cref="LiquidSimJob"/>.
    /// Designed for future lava support — water and lava differ in
    /// max level, BFS search radius, and source neighbor threshold.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LiquidJobConfig
    {
        /// <summary>StateId.Value for the source block of this fluid (level=0 in content).</summary>
        public ushort SourceStateId;

        /// <summary>StateId.Value offset: flowing level N = SourceStateId + N.</summary>
        public ushort BaseFlowingStateId;

        /// <summary>Maximum flowing level (7 for water, 3 for lava).</summary>
        public byte MaxLevel;

        /// <summary>BFS horizontal search radius for flow direction (4 for water, 2 for lava).</summary>
        public byte FlowSearchRadius;

        /// <summary>
        /// Number of horizontal source neighbors required to create a new source.
        /// 2 for water (infinite source rule). 255 for lava (effectively never).
        /// </summary>
        public byte SourceNeighborThreshold;

        /// <summary>Fluid type identifier.</summary>
        public LiquidFluidType FluidType;

        /// <summary>Creates the default water configuration.</summary>
        public static LiquidJobConfig Water(ushort sourceStateId)
        {
            return new LiquidJobConfig
            {
                SourceStateId = sourceStateId,
                BaseFlowingStateId = (ushort)(sourceStateId + 1),
                MaxLevel = LiquidCell.MaxFlowingLevel,
                FlowSearchRadius = 4,
                SourceNeighborThreshold = 2,
                FluidType = LiquidFluidType.Water,
            };
        }
    }
}
