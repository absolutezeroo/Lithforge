using System;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Computes UV rectangles for body part faces in a 64x64 Minecraft skin texture.
    /// Minecraft skin layout: each part is a T-shaped strip of 6 faces packed at a known origin.
    /// </summary>
    public static class SkinUVMapper
    {
        private const float TexSize = 64f;

        // Head (8x8x8)
        public static readonly SkinPartDefinition HeadBase = new SkinPartDefinition(0, 0, 8, 8, 8);
        public static readonly SkinPartDefinition HeadOverlay = new SkinPartDefinition(32, 0, 8, 8, 8);

        // Body (8x12x4)
        public static readonly SkinPartDefinition BodyBase = new SkinPartDefinition(16, 16, 8, 12, 4);
        public static readonly SkinPartDefinition BodyOverlay = new SkinPartDefinition(16, 32, 8, 12, 4);

        // Right Arm (classic 4px wide)
        public static readonly SkinPartDefinition RightArmBase4 = new SkinPartDefinition(40, 16, 4, 12, 4);
        public static readonly SkinPartDefinition RightArmOverlay4 = new SkinPartDefinition(40, 32, 4, 12, 4);

        // Right Arm (slim 3px wide)
        public static readonly SkinPartDefinition RightArmBase3 = new SkinPartDefinition(40, 16, 3, 12, 4);
        public static readonly SkinPartDefinition RightArmOverlay3 = new SkinPartDefinition(40, 32, 3, 12, 4);

        // Left Arm (classic 4px wide)
        public static readonly SkinPartDefinition LeftArmBase4 = new SkinPartDefinition(32, 48, 4, 12, 4);
        public static readonly SkinPartDefinition LeftArmOverlay4 = new SkinPartDefinition(48, 48, 4, 12, 4);

        // Left Arm (slim 3px wide)
        public static readonly SkinPartDefinition LeftArmBase3 = new SkinPartDefinition(32, 48, 3, 12, 4);
        public static readonly SkinPartDefinition LeftArmOverlay3 = new SkinPartDefinition(48, 48, 3, 12, 4);

        // Right Leg (4x12x4)
        public static readonly SkinPartDefinition RightLegBase = new SkinPartDefinition(0, 16, 4, 12, 4);
        public static readonly SkinPartDefinition RightLegOverlay = new SkinPartDefinition(0, 32, 4, 12, 4);

        // Left Leg (4x12x4)
        public static readonly SkinPartDefinition LeftLegBase = new SkinPartDefinition(16, 48, 4, 12, 4);
        public static readonly SkinPartDefinition LeftLegOverlay = new SkinPartDefinition(0, 48, 4, 12, 4);

        /// <summary>
        /// Computes the UV rectangle for a specific face of a body part.
        /// Returns (uMin, vMin, uMax, vMax) in [0,1] normalized coordinates.
        /// Uses top-left origin (Minecraft convention). The shader flips Y as needed.
        /// </summary>
        public static Vector4 GetFaceUV(SkinPartDefinition part, SkinFaceDirection face)
        {
            int u = part.OriginU;
            int v = part.OriginV;
            int w = part.W;
            int h = part.H;
            int d = part.D;

            int px;
            int py;
            int pw;
            int ph;

            switch (face)
            {
                case SkinFaceDirection.Top:
                    px = u + d;
                    py = v;
                    pw = w;
                    ph = d;
                    break;
                case SkinFaceDirection.Bottom:
                    px = u + d + w;
                    py = v;
                    pw = w;
                    ph = d;
                    break;
                case SkinFaceDirection.Right:
                    px = u;
                    py = v + d;
                    pw = d;
                    ph = h;
                    break;
                case SkinFaceDirection.Front:
                    px = u + d;
                    py = v + d;
                    pw = w;
                    ph = h;
                    break;
                case SkinFaceDirection.Left:
                    px = u + d + w;
                    py = v + d;
                    pw = d;
                    ph = h;
                    break;
                case SkinFaceDirection.Back:
                    px = u + d + w + d;
                    py = v + d;
                    pw = w;
                    ph = h;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(face));
            }

            return new Vector4(
                px / TexSize,
                py / TexSize,
                (px + pw) / TexSize,
                (py + ph) / TexSize);
        }
    }
}
