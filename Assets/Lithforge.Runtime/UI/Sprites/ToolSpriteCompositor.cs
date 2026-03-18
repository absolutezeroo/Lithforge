using System;

using Lithforge.Runtime.Content.Tools;
using Lithforge.Item;
using Lithforge.Voxel.Item;

using UnityEngine;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.UI.Sprites
{
    /// <summary>
    ///     Composites tool part layer textures into a single sprite.
    ///     Layer order and part-to-layer mapping are driven by ToolDefinition.
    /// </summary>
    public static class ToolSpriteCompositor
    {
        private const int SpriteSize = 32;

        /// <summary>
        ///     Creates a composite sprite for a ToolInstance by layering its part textures.
        ///     Layer order is defined by the ToolDefinition's spriteLayers array (bottom to top).
        ///     Returns null if no layers could be resolved.
        /// </summary>
        public static Sprite Composite(
            ToolInstance tool,
            ToolPartTextureDatabase texDb)
        {
            if (tool == null || tool.Parts == null || tool.Parts.Length == 0)
            {
                return null;
            }

            ToolDefinition def = texDb.GetDefinition(tool.ToolType);

            if (def == null || def.spriteLayers == null || def.spriteLayers.Length == 0)
            {
                return null;
            }

            // Collect layers in the order defined by the SO (bottom to top)
            Texture2D[] layers = new Texture2D[def.spriteLayers.Length];
            bool anyLayer = false;

            for (int l = 0; l < def.spriteLayers.Length; l++)
            {
                SpriteLayer layerDef = def.spriteLayers[l];

                // Find the part that matches this layer
                if (layerDef.partTypes == null || layerDef.partTypes.Length == 0)
                {
                    continue;
                }

                ToolPart? matchedPart = null;

                for (int p = 0; p < tool.Parts.Length; p++)
                {
                    ToolPart part = tool.Parts[p];

                    for (int pt = 0; pt < layerDef.partTypes.Length; pt++)
                    {
                        if (part.PartType == layerDef.partTypes[pt])
                        {
                            matchedPart = part;
                            break;
                        }
                    }

                    if (matchedPart.HasValue)
                    {
                        break;
                    }
                }

                if (!matchedPart.HasValue)
                {
                    continue;
                }

                string matSuffix = texDb.ResolveSuffix(matchedPart.Value.MaterialId);

                Texture2D tex = texDb.GetLayer(
                    tool.ToolType, layerDef.textureSubfolder, matSuffix);

                if (tex != null)
                {
                    layers[l] = tex;
                    anyLayer = true;
                }
            }

            if (!anyLayer)
            {
                return null;
            }

            Texture2D composite = CompositeLayersPixel(layers, SpriteSize);

            return Sprite.Create(
                composite,
                new Rect(0, 0, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                SpriteSize);
        }

        /// <summary>
        ///     Alpha-over compositing of multiple layers onto a single texture.
        ///     Null entries in the array are skipped.
        ///     Uses RenderTexture blit to handle non-readable source textures.
        /// </summary>
        private static Texture2D CompositeLayersPixel(Texture2D[] layers, int size)
        {
            Texture2D result = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            Color32[] pixels = new Color32[size * size];

            // Start fully transparent (Color32 zero-initializes to (0,0,0,0))
            Array.Clear(pixels, 0, pixels.Length);

            // Reusable scratch texture for reading non-readable source textures
            Texture2D scratch = new(size, size, TextureFormat.RGBA32, false);

            for (int l = 0; l < layers.Length; l++)
            {
                if (layers[l] == null)
                {
                    continue;
                }

                // Blit through RenderTexture (source textures may be non-readable)
                // Force Point filtering to prevent bilinear blur on pixel art
                FilterMode originalFilter = layers[l].filterMode;
                layers[l].filterMode = FilterMode.Point;

                RenderTexture rt = RenderTexture.GetTemporary(
                    size, size, 0, RenderTextureFormat.ARGB32);
                rt.filterMode = FilterMode.Point;
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(layers[l], rt);
                RenderTexture.active = rt;

                scratch.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                scratch.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                layers[l].filterMode = originalFilter;

                Color32[] layerPixels = scratch.GetPixels32();

                // Alpha-over compositing: src over dst
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 src = layerPixels[i];

                    if (src.a == 0)
                    {
                        continue;
                    }

                    Color32 dst = pixels[i];
                    float sa = src.a / 255f;
                    float da = dst.a / 255f;
                    float outA = sa + da * (1f - sa);

                    if (outA > 0f)
                    {
                        float invOutA = 1f / outA;
                        float oneMinusSa = 1f - sa;
                        pixels[i] = new Color32(
                            (byte)((src.r * sa + dst.r * da * oneMinusSa) * invOutA),
                            (byte)((src.g * sa + dst.g * da * oneMinusSa) * invOutA),
                            (byte)((src.b * sa + dst.b * da * oneMinusSa) * invOutA),
                            (byte)(outA * 255f));
                    }
                }
            }

            Object.Destroy(scratch);

            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }
    }
}
