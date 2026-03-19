using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;

namespace Lithforge.WorldGen.Ore
{
    /// <summary>Blittable ore generation parameters baked from OreDefinition for Burst jobs.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeOreConfig
    {
        /// <summary>Block state to place when ore is generated.</summary>
        public StateId OreStateId;

        /// <summary>Block state that this ore replaces (typically stone).</summary>
        public StateId ReplaceStateId;

        /// <summary>Minimum Y level for ore generation.</summary>
        public int MinY;

        /// <summary>Maximum Y level for ore generation.</summary>
        public int MaxY;

        /// <summary>Maximum number of blocks per ore vein.</summary>
        public int VeinSize;

        /// <summary>Number of ore veins attempted per chunk.</summary>
        public float Frequency;

        /// <summary>Ore generation type (0=blob, 1=scatter).</summary>
        public byte OreType;
    }
}
