using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;

namespace Lithforge.WorldGen.Ore
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeOreConfig
    {
        public StateId OreStateId;
        public StateId ReplaceStateId;
        public int MinY;
        public int MaxY;
        public int VeinSize;
        public float Frequency;
        public byte OreType;
    }
}
