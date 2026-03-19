namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Defines a body part's region in the 64x64 Minecraft skin texture.
    /// </summary>
    public readonly struct SkinPartDefinition
    {
        /// <summary>Pixel X origin of the T-shaped strip (top-left origin).</summary>
        public readonly int OriginU;

        /// <summary>Pixel Y origin of the T-shaped strip (top-left origin).</summary>
        public readonly int OriginV;

        /// <summary>Box width in pixels.</summary>
        public readonly int W;

        /// <summary>Box height in pixels.</summary>
        public readonly int H;

        /// <summary>Box depth in pixels.</summary>
        public readonly int D;

        /// <summary>Creates a part definition with the given UV origin and box dimensions.</summary>
        public SkinPartDefinition(int originU, int originV, int w, int h, int d)
        {
            OriginU = originU;
            OriginV = originV;
            W = w;
            H = h;
            D = d;
        }
    }
}
