using System;

using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    ///     Computes UV rectangles for body part faces in a 64x64 Minecraft skin texture.
    ///     Minecraft skin layout: each part is a T-shaped strip of 6 faces packed at a known origin.
    /// </summary>
    public static class SkinUVMapper
    {
        /// <summary>Skin texture dimension in pixels (64x64).</summary>
        private const float TexSize = 64f;

        /// <summary>Head base layer UV definition (8x8x8 box at origin 0,0).</summary>
        public static readonly SkinPartDefinition HeadBase = new(0, 0, 8, 8, 8);

        /// <summary>Head overlay (hat) layer UV definition (8x8x8 box at origin 32,0).</summary>
        public static readonly SkinPartDefinition HeadOverlay = new(32, 0, 8, 8, 8);

        /// <summary>Body base layer UV definition (8x12x4 box at origin 16,16).</summary>
        public static readonly SkinPartDefinition BodyBase = new(16, 16, 8, 12, 4);

        /// <summary>Body overlay (jacket) layer UV definition (8x12x4 box at origin 16,32).</summary>
        public static readonly SkinPartDefinition BodyOverlay = new(16, 32, 8, 12, 4);

        /// <summary>Right arm base layer UV definition for classic 4-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition RightArmBase4 = new(40, 16, 4, 12, 4);

        /// <summary>Right arm overlay (sleeve) layer UV definition for classic 4-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition RightArmOverlay4 = new(40, 32, 4, 12, 4);

        /// <summary>Right arm base layer UV definition for slim 3-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition RightArmBase3 = new(40, 16, 3, 12, 4);

        /// <summary>Right arm overlay (sleeve) layer UV definition for slim 3-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition RightArmOverlay3 = new(40, 32, 3, 12, 4);

        /// <summary>Left arm base layer UV definition for classic 4-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition LeftArmBase4 = new(32, 48, 4, 12, 4);

        /// <summary>Left arm overlay (sleeve) layer UV definition for classic 4-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition LeftArmOverlay4 = new(48, 48, 4, 12, 4);

        /// <summary>Left arm base layer UV definition for slim 3-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition LeftArmBase3 = new(32, 48, 3, 12, 4);

        /// <summary>Left arm overlay (sleeve) layer UV definition for slim 3-pixel-wide skins.</summary>
        public static readonly SkinPartDefinition LeftArmOverlay3 = new(48, 48, 3, 12, 4);

        /// <summary>Right leg base layer UV definition (4x12x4 box at origin 0,16).</summary>
        public static readonly SkinPartDefinition RightLegBase = new(0, 16, 4, 12, 4);

        /// <summary>Right leg overlay (pants) layer UV definition (4x12x4 box at origin 0,32).</summary>
        public static readonly SkinPartDefinition RightLegOverlay = new(0, 32, 4, 12, 4);

        /// <summary>Left leg base layer UV definition (4x12x4 box at origin 16,48).</summary>
        public static readonly SkinPartDefinition LeftLegBase = new(16, 48, 4, 12, 4);

        /// <summary>Left leg overlay (pants) layer UV definition (4x12x4 box at origin 0,48).</summary>
        public static readonly SkinPartDefinition LeftLegOverlay = new(0, 48, 4, 12, 4);

        /// <summary>
        ///     Computes the UV rectangle for a specific face of a body part.
        ///     Returns (uMin, vMin, uMax, vMax) in [0,1] normalized coordinates.
        ///     Uses top-left origin (Minecraft convention). The shader flips Y as needed.
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
