using System.IO;

using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    ///     Loads 64x64 Minecraft-compatible skin textures from StreamingAssets/skins/.
    ///     Supports both modern 64x64 and legacy 64x32 formats (auto-converts legacy
    ///     by mirroring right limbs to left limb positions).
    /// </summary>
    public sealed class SkinLoader
    {
        /// <summary>Subfolder name within StreamingAssets where skin PNGs are stored.</summary>
        private const string SkinsFolder = "Skins";

        /// <summary>
        ///     Loads a skin PNG from StreamingAssets/skins/{filename}.
        ///     Returns null if the file is not found or has invalid dimensions.
        /// </summary>
        public Texture2D LoadSkin(string filename)
        {
            string path = Path.Combine(Application.streamingAssetsPath, SkinsFolder, filename);

            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogWarning($"[SkinLoader] Skin not found: {path}");
                return null;
            }

            byte[] pngBytes = File.ReadAllBytes(path);
            Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);

            if (!tex.LoadImage(pngBytes))
            {
                Object.Destroy(tex);
                UnityEngine.Debug.LogWarning($"[SkinLoader] Failed to decode skin image: {path}");
                return null;
            }

            if (tex.width != 64 || tex.height != 64 && tex.height != 32)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SkinLoader] Invalid skin dimensions: {tex.width}x{tex.height} (expected 64x64 or 64x32)");
                Object.Destroy(tex);
                return null;
            }

            if (tex.height == 32)
            {
                tex = ConvertLegacySkin(tex);
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = Path.GetFileNameWithoutExtension(filename);
            return tex;
        }

        /// <summary>
        ///     Generates a default Steve skin texture (solid colored regions).
        ///     Used as a fallback when no skin file is available.
        /// </summary>
        public Texture2D CreateDefaultSkin()
        {
            Texture2D tex = new(64, 64, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[64 * 64];

            // Fill with transparent
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }

            Color skinColor = new(0.73f, 0.56f, 0.42f, 1f);
            Color shirtColor = new(0.22f, 0.68f, 0.68f, 1f);
            Color pantsColor = new(0.27f, 0.27f, 0.75f, 1f);
            Color hairColor = new(0.35f, 0.22f, 0.12f, 1f);

            // Head base (u=0, v=0, 8w 8h 8d → strip 32x16)
            FillRegion(pixels, 8, 0, 8, 8, skinColor);  // top face
            FillRegion(pixels, 16, 0, 8, 8, skinColor); // bottom face
            FillRegion(pixels, 0, 8, 8, 8, hairColor);  // right face
            FillRegion(pixels, 8, 8, 8, 8, skinColor);  // front face
            FillRegion(pixels, 16, 8, 8, 8, hairColor); // left face
            FillRegion(pixels, 24, 8, 8, 8, hairColor); // back face

            // Body base (u=16, v=16, 8w 12h 4d → strip 24x16)
            FillRegion(pixels, 20, 16, 8, 4, shirtColor);  // top
            FillRegion(pixels, 28, 16, 8, 4, shirtColor);  // bottom
            FillRegion(pixels, 16, 20, 4, 12, shirtColor); // right
            FillRegion(pixels, 20, 20, 8, 12, shirtColor); // front
            FillRegion(pixels, 28, 20, 4, 12, shirtColor); // left
            FillRegion(pixels, 32, 20, 8, 12, shirtColor); // back

            // Right arm base (u=40, v=16, 4w 12h 4d → strip 16x16)
            FillRegion(pixels, 44, 16, 4, 4, skinColor);   // top
            FillRegion(pixels, 48, 16, 4, 4, skinColor);   // bottom
            FillRegion(pixels, 40, 20, 4, 12, shirtColor); // right
            FillRegion(pixels, 44, 20, 4, 12, shirtColor); // front
            FillRegion(pixels, 48, 20, 4, 12, shirtColor); // left
            FillRegion(pixels, 52, 20, 4, 12, shirtColor); // back

            // Left arm base (u=32, v=48, 4w 12h 4d → strip 16x16)
            FillRegion(pixels, 36, 48, 4, 4, skinColor);
            FillRegion(pixels, 40, 48, 4, 4, skinColor);
            FillRegion(pixels, 32, 52, 4, 12, shirtColor);
            FillRegion(pixels, 36, 52, 4, 12, shirtColor);
            FillRegion(pixels, 40, 52, 4, 12, shirtColor);
            FillRegion(pixels, 44, 52, 4, 12, shirtColor);

            // Right leg base (u=0, v=16, 4w 12h 4d → strip 16x16)
            FillRegion(pixels, 4, 16, 4, 4, pantsColor);
            FillRegion(pixels, 8, 16, 4, 4, pantsColor);
            FillRegion(pixels, 0, 20, 4, 12, pantsColor);
            FillRegion(pixels, 4, 20, 4, 12, pantsColor);
            FillRegion(pixels, 8, 20, 4, 12, pantsColor);
            FillRegion(pixels, 12, 20, 4, 12, pantsColor);

            // Left leg base (u=16, v=48, 4w 12h 4d → strip 16x16)
            FillRegion(pixels, 20, 48, 4, 4, pantsColor);
            FillRegion(pixels, 24, 48, 4, 4, pantsColor);
            FillRegion(pixels, 16, 52, 4, 12, pantsColor);
            FillRegion(pixels, 20, 52, 4, 12, pantsColor);
            FillRegion(pixels, 24, 52, 4, 12, pantsColor);
            FillRegion(pixels, 28, 52, 4, 12, pantsColor);

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "DefaultSkin";
            return tex;
        }

        /// <summary>
        ///     Converts a legacy 64x32 skin to 64x64 by mirroring right limbs to left positions.
        ///     Unity loads PNGs with Y=0 at bottom, so row 0 in skin coords is row 63 in pixel coords.
        /// </summary>
        private Texture2D ConvertLegacySkin(Texture2D legacy)
        {
            Texture2D full = new(64, 64, TextureFormat.RGBA32, false);
            Color[] clear = new Color[64 * 64];
            full.SetPixels(clear);

            // Copy top 32 rows of legacy skin to top 32 rows of full skin.
            // In Unity pixel coords (Y=0 bottom): legacy rows [0..31] → full rows [32..63]
            Color[] topHalf = legacy.GetPixels(0, 0, 64, 32);
            full.SetPixels(0, 32, 64, 32, topHalf);

            // Mirror right arm → left arm (base)
            // Right arm base: skin (u=40, v=16) → Left arm base: skin (u=32, v=48)
            MirrorLimbRegion(full, legacy, 40, 16, 32, 48, 4, 12, 4);

            // Mirror right leg → left leg (base)
            // Right leg base: skin (u=0, v=16) → Left leg base: skin (u=16, v=48)
            MirrorLimbRegion(full, legacy, 0, 16, 16, 48, 4, 12, 4);

            full.Apply();
            Object.Destroy(legacy);
            return full;
        }

        /// <summary>
        ///     Mirrors a limb's T-shaped UV region from src coords to dst coords
        ///     with horizontal flip for Front/Back faces and Left/Right swap.
        ///     All coordinates use skin-space (top-left origin).
        /// </summary>
        private void MirrorLimbRegion(
            Texture2D dst, Texture2D src,
            int srcU, int srcV, int dstU, int dstV,
            int w, int h, int d)
        {
            // The T-shaped strip has two rows of faces:
            // Row 1 (top): Top face, Bottom face
            // Row 2 (body): Right, Front, Left, Back faces

            // Top face (srcU+d, srcV, w×d) → (dstU+d, dstV, w×d) — flip horizontal
            CopyRegionFlippedH(dst, src, srcU + d, srcV, dstU + d, dstV, w, d);

            // Bottom face (srcU+d+w, srcV, w×d) → (dstU+d+w, dstV, w×d) — flip horizontal
            CopyRegionFlippedH(dst, src, srcU + d + w, srcV, dstU + d + w, dstV, w, d);

            // Right face (srcU, srcV+d, d×h) → Left face destination (dstU+d+w, dstV+d, d×h)
            CopyRegionFlippedH(dst, src, srcU, srcV + d, dstU + d + w, dstV + d, d, h);

            // Front face (srcU+d, srcV+d, w×h) → (dstU+d, dstV+d, w×h) — flip horizontal
            CopyRegionFlippedH(dst, src, srcU + d, srcV + d, dstU + d, dstV + d, w, h);

            // Left face (srcU+d+w, srcV+d, d×h) → Right face destination (dstU, dstV+d, d×h)
            CopyRegionFlippedH(dst, src, srcU + d + w, srcV + d, dstU, dstV + d, d, h);

            // Back face (srcU+d+w+d, srcV+d, w×h) → (dstU+d+w+d, dstV+d, w×h) — flip horizontal
            CopyRegionFlippedH(dst, src, srcU + d + w + d, srcV + d, dstU + d + w + d, dstV + d, w, h);
        }

        /// <summary>
        ///     Copies a rectangular region from src to dst with horizontal flip.
        ///     All coordinates use skin-space (top-left origin, Y increases downward).
        ///     Converts to Unity pixel-space internally (Y=0 at bottom).
        /// </summary>
        private void CopyRegionFlippedH(
            Texture2D dst, Texture2D src,
            int srcX, int srcY, int dstX, int dstY,
            int width, int height)
        {
            int srcTexH = src.height;
            int dstTexH = dst.height;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    // Skin-space to Unity pixel-space: pixelY = texHeight - 1 - skinY
                    int srcPixelX = srcX + col;
                    int srcPixelY = srcTexH - 1 - (srcY + row);

                    int dstPixelX = dstX + (width - 1 - col); // horizontal flip
                    int dstPixelY = dstTexH - 1 - (dstY + row);

                    Color pixel = src.GetPixel(srcPixelX, srcPixelY);
                    dst.SetPixel(dstPixelX, dstPixelY, pixel);
                }
            }
        }

        /// <summary>
        ///     Fills a rectangular region in pixel array using skin-space coordinates.
        /// </summary>
        private static void FillRegion(Color[] pixels, int skinX, int skinY, int width, int height, Color color)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    // Skin-space Y (top-left) → Unity pixel index (Y=0 bottom, row-major)
                    int pixelY = 63 - (skinY + row);
                    int pixelX = skinX + col;
                    pixels[pixelY * 64 + pixelX] = color;
                }
            }
        }
    }
}
