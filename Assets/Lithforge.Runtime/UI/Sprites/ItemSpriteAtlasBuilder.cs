using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Models;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;

using UnityEngine;

namespace Lithforge.Runtime.UI.Sprites
{
    /// <summary>
    ///     Builds an ItemSpriteAtlas from item textures and block top-face textures.
    ///     Standalone items use textures from Content/Textures/Items/.
    ///     Block items use the top face texture from resolvedFaces.
    ///     Tool parts use tag-based resolution via ToolPartTextureDatabase.
    ///     Uses the same RenderTexture blit pattern as AtlasBuilder for non-readable textures.
    /// </summary>
    public static class ItemSpriteAtlasBuilder
    {
        private const int SpriteSize = 32;

        /// <summary>
        ///     Builds the sprite atlas from item entries and resolved block face textures.
        /// </summary>
        public static ItemSpriteAtlas Build(
            List<ItemEntry> itemEntries,
            StateRegistry stateRegistry,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces,
            ToolPartTextureDatabase toolTexDb)
        {
            Dictionary<ResourceId, Sprite> sprites = new();

            // Load all item textures from Resources
            Texture2D[] itemTextures = Resources.LoadAll<Texture2D>("Content/Textures/Items");
            Dictionary<string, Texture2D> itemTexturesByName = new();

            for (int i = 0; i < itemTextures.Length; i++)
            {
                itemTexturesByName[itemTextures[i].name] = itemTextures[i];
            }

            // Build sprites for all item entries
            for (int i = 0; i < itemEntries.Count; i++)
            {
                ItemEntry entry = itemEntries[i];
                ResourceId itemId = entry.Id;

                // Try standalone item texture first (by item name)
                if (itemTexturesByName.TryGetValue(itemId.Name, out Texture2D itemTex))
                {
                    Sprite sprite = CreateSpriteFromTexture(itemTex);

                    if (sprite != null)
                    {
                        sprites[itemId] = sprite;
                        continue;
                    }
                }

                // Fallback: resolve tool part texture from item tags
                if (toolTexDb != null && entry.Tags != null)
                {
                    ResourceId materialId = default;
                    bool hasMaterial = false;

                    // Extract material ResourceId from tags
                    for (int t = 0; t < entry.Tags.Count; t++)
                    {
                        string tag = entry.Tags[t];

                        if (tag.StartsWith("material:") &&
                            ResourceId.TryParse(tag.Substring("material:".Length), out ResourceId matId))
                        {
                            materialId = matId;
                            hasMaterial = true;
                        }
                    }

                    if (hasMaterial)
                    {
                        // Resolve suffix through ToolPartTextureDatabase (respects textureSuffix overrides)
                        string materialSuffix = toolTexDb.ResolveSuffix(materialId);

                        // Determine part type from tags, then find matching layer
                        ToolPartType tagPartType = ResolvePartTypeFromTags(entry.Tags);

                        if (tagPartType != ToolPartType.None)
                        {
                            Texture2D partTex = toolTexDb.FindPartTexture(tagPartType, materialSuffix);

                            if (partTex != null)
                            {
                                sprites[itemId] = CreateSpriteFromTexture(partTex);
                            }
                        }
                    }
                }
            }

            // Build sprites for block items (using top face from resolved faces)
            IReadOnlyList<StateRegistryEntry> stateEntries = stateRegistry.Entries;

            for (int i = 0; i < stateEntries.Count; i++)
            {
                StateRegistryEntry stateEntry = stateEntries[i];
                ResourceId blockId = stateEntry.Id;

                // Block item id matches block id
                if (sprites.ContainsKey(blockId))
                {
                    continue; // Already has a standalone texture
                }

                StateId baseState = new(stateEntry.BaseStateId);

                if (resolvedFaces.TryGetValue(baseState, out ResolvedFaceTextures2D faces))
                {
                    Texture2D topFace = faces.Up;
                    Texture2D leftFace = faces.South;
                    Texture2D rightFace = faces.East;

                    if (topFace != null)
                    {
                        if (leftFace == null)
                        {
                            leftFace = topFace;
                        }

                        if (rightFace == null)
                        {
                            rightFace = topFace;
                        }

                        Sprite sprite = CreateIsometricBlockSprite(topFace, leftFace, rightFace);

                        if (sprite != null)
                        {
                            sprites[blockId] = sprite;
                        }
                    }
                }
            }

            // Build fallback sprite (magenta checkerboard)
            Sprite fallback = CreateFallbackSprite();

            return new ItemSpriteAtlas(sprites, fallback);
        }

        private static ToolPartType ResolvePartTypeFromTags(List<string> tags)
        {
            for (int t = 0; t < tags.Count; t++)
            {
                switch (tags[t])
                {
                    case "tool_part_head":
                        return ToolPartType.Head;
                    case "tool_part_blade":
                        return ToolPartType.Blade;
                    case "tool_part_handle":
                        return ToolPartType.Handle;
                    case "tool_part_binding":
                        return ToolPartType.Binding;
                    case "tool_part_guard":
                        return ToolPartType.Guard;
                    case "tool_part_point":
                        return ToolPartType.Point;
                    case "tool_part_shaft":
                        return ToolPartType.Shaft;
                }
            }

            return ToolPartType.None;
        }

        private static Sprite CreateSpriteFromTexture(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            // Blit through RenderTexture to handle non-readable textures.
            // Force Point filtering on source to prevent bilinear blur on pixel art.
            FilterMode originalFilter = source.filterMode;
            source.filterMode = FilterMode.Point;

            Texture2D readable = new(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            readable.filterMode = FilterMode.Point;

            RenderTexture rt = RenderTexture.GetTemporary(
                SpriteSize, SpriteSize, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, SpriteSize, SpriteSize), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // Restore original filter mode to avoid side effects on shared textures
            source.filterMode = originalFilter;

            Sprite sprite = Sprite.Create(
                readable,
                new Rect(0, 0, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                SpriteSize);

            return sprite;
        }

        private static Sprite CreateFallbackSprite()
        {
            Texture2D fallbackTex = new(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            fallbackTex.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[SpriteSize * SpriteSize];

            for (int p = 0; p < pixels.Length; p++)
            {
                int px = p % SpriteSize;
                int py = p / SpriteSize;
                bool checker = (px / 4 + py / 4) % 2 == 0;
                pixels[p] = checker
                    ? new Color32(255, 0, 255, 255)
                    : new Color32(0, 0, 0, 255);
            }

            fallbackTex.SetPixels32(pixels);
            fallbackTex.Apply();

            return Sprite.Create(
                fallbackTex,
                new Rect(0, 0, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                SpriteSize);
        }

        /// <summary>
        ///     Renders a Minecraft-style isometric block sprite from 3 face textures.
        ///     Top face rendered as a diamond, left (south) and right (east) faces
        ///     as parallelograms with darkening for depth illusion.
        ///     Output is 32x32 with FilterMode.Point for pixel art.
        /// </summary>
        private static Sprite CreateIsometricBlockSprite(
            Texture2D topTex, Texture2D leftTex, Texture2D rightTex)
        {
            const int isoSize = 32;
            const int facePixels = 16;

            Texture2D result = new Texture2D(isoSize, isoSize, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[isoSize * isoSize];

            Color32 clear = new Color32(0, 0, 0, 0);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            Color32[] topPixels = ReadPixelsAtSize(topTex, facePixels);
            Color32[] leftPixels = ReadPixelsAtSize(leftTex, facePixels);
            Color32[] rightPixels = ReadPixelsAtSize(rightTex, facePixels);

            int centerX = isoSize / 2;
            int topY = isoSize - 1;

            // --- TOP FACE (diamond) ---
            for (int ty = 0; ty < facePixels; ty++)
            {
                for (int tx = 0; tx < facePixels; tx++)
                {
                    int sx = centerX + (tx - ty) - 1;
                    int sy = topY - (tx + ty) / 2;

                    Color32 c = topPixels[ty * facePixels + tx];

                    if (c.a == 0)
                    {
                        continue;
                    }

                    SetPixelSafe(pixels, isoSize, sx, sy, c);
                    SetPixelSafe(pixels, isoSize, sx + 1, sy, c);
                }
            }

            // --- LEFT FACE (south texture, darker) ---
            const float leftDarken = 0.75f;
            int leftBaseY = topY - facePixels / 2;

            for (int ty = 0; ty < facePixels; ty++)
            {
                for (int tx = 0; tx < facePixels; tx++)
                {
                    int sx = centerX - facePixels + tx;
                    int sy = leftBaseY - ty / 2 - tx;

                    Color32 c = leftPixels[ty * facePixels + tx];

                    if (c.a == 0)
                    {
                        continue;
                    }

                    c = DarkenColor(c, leftDarken);
                    SetPixelSafe(pixels, isoSize, sx, sy, c);

                    if (ty % 2 == 1)
                    {
                        SetPixelSafe(pixels, isoSize, sx, sy - 1, c);
                    }
                }
            }

            // --- RIGHT FACE (east texture, darkest) ---
            const float rightDarken = 0.55f;
            int rightBaseY = topY - facePixels / 2;

            for (int ty = 0; ty < facePixels; ty++)
            {
                for (int tx = 0; tx < facePixels; tx++)
                {
                    int sx = centerX + tx;
                    int sy = rightBaseY - ty / 2 - (facePixels - 1 - tx);

                    Color32 c = rightPixels[ty * facePixels + tx];

                    if (c.a == 0)
                    {
                        continue;
                    }

                    c = DarkenColor(c, rightDarken);
                    SetPixelSafe(pixels, isoSize, sx, sy, c);

                    if (ty % 2 == 1)
                    {
                        SetPixelSafe(pixels, isoSize, sx, sy - 1, c);
                    }
                }
            }

            result.SetPixels32(pixels);
            result.Apply();

            return Sprite.Create(
                result,
                new Rect(0, 0, isoSize, isoSize),
                new Vector2(0.5f, 0.5f),
                isoSize);
        }

        /// <summary>
        ///     Reads pixels from a texture at the given size via RenderTexture blit.
        ///     Handles non-readable textures. Returns Color32 array in bottom-to-top order.
        /// </summary>
        private static Color32[] ReadPixelsAtSize(Texture2D source, int size)
        {
            if (source == null)
            {
                return new Color32[size * size];
            }

            FilterMode originalFilter = source.filterMode;
            source.filterMode = FilterMode.Point;

            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            source.filterMode = originalFilter;

            Color32[] result = readable.GetPixels32();
            Object.DestroyImmediate(readable);
            return result;
        }

        private static void SetPixelSafe(Color32[] pixels, int size, int x, int y, Color32 c)
        {
            if (x >= 0 && x < size && y >= 0 && y < size)
            {
                pixels[y * size + x] = c;
            }
        }

        private static Color32 DarkenColor(Color32 c, float factor)
        {
            return new Color32(
                (byte)(c.r * factor),
                (byte)(c.g * factor),
                (byte)(c.b * factor),
                c.a);
        }
    }
}
