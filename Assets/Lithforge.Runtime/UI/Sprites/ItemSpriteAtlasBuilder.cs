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
        ///     Renders a Luanti-style isometric cube from 3 face textures.
        ///     Algorithm ported from Luanti (minetest/src/client/imagesource.cpp).
        ///     Each face is drawn via an affine transform with a per-texel pixel pattern.
        ///     Output size = 9 * tileSize (144x144 for 16px tiles).
        /// </summary>
        private static Sprite CreateIsometricBlockSprite(
            Texture2D topTex, Texture2D leftTex, Texture2D rightTex)
        {
            const int tileSize = 16;
            int cubeSize = 9 * tileSize;
            int xOffset = tileSize / 2;

            Color32[] pixels = new Color32[cubeSize * cubeSize];

            Color32[] topPx = ReadPixelsPoint(topTex, tileSize);
            Color32[] leftPx = ReadPixelsPoint(leftTex, tileSize);
            Color32[] rightPx = ReadPixelsPoint(rightTex, tileSize);

            // Top face (brightness 1.0)
            int[][] topPattern =
            {
                new[]
                {
                    2,
                    0,
                },
                new[]
                {
                    3,
                    0,
                },
                new[]
                {
                    4,
                    0,
                },
                new[]
                {
                    5,
                    0,
                },
                new[]
                {
                    0,
                    1,
                },
                new[]
                {
                    1,
                    1,
                },
                new[]
                {
                    2,
                    1,
                },
                new[]
                {
                    3,
                    1,
                },
                new[]
                {
                    4,
                    1,
                },
                new[]
                {
                    5,
                    1,
                },
                new[]
                {
                    6,
                    1,
                },
                new[]
                {
                    7,
                    1,
                },
                new[]
                {
                    2,
                    2,
                },
                new[]
                {
                    3,
                    2,
                },
                new[]
                {
                    4,
                    2,
                },
                new[]
                {
                    5,
                    2,
                },
            };

            DrawCubeFace(pixels, cubeSize, topPx, tileSize, 1.0f,
                4, -4, 4 * (tileSize - 1),
                2, 2, 0,
                xOffset, topPattern);

            // Left face (brightness 0.836660)
            int[][] leftPattern =
            {
                new[]
                {
                    0,
                    0,
                },
                new[]
                {
                    1,
                    0,
                },
                new[]
                {
                    0,
                    1,
                },
                new[]
                {
                    1,
                    1,
                },
                new[]
                {
                    2,
                    1,
                },
                new[]
                {
                    3,
                    1,
                },
                new[]
                {
                    0,
                    2,
                },
                new[]
                {
                    1,
                    2,
                },
                new[]
                {
                    2,
                    2,
                },
                new[]
                {
                    3,
                    2,
                },
                new[]
                {
                    0,
                    3,
                },
                new[]
                {
                    1,
                    3,
                },
                new[]
                {
                    2,
                    3,
                },
                new[]
                {
                    3,
                    3,
                },
                new[]
                {
                    0,
                    4,
                },
                new[]
                {
                    1,
                    4,
                },
                new[]
                {
                    2,
                    4,
                },
                new[]
                {
                    3,
                    4,
                },
                new[]
                {
                    2,
                    5,
                },
                new[]
                {
                    3,
                    5,
                },
            };

            DrawCubeFace(pixels, cubeSize, leftPx, tileSize, 0.836660f,
                4, 0, 0,
                2, 5, 2 * tileSize,
                xOffset, leftPattern);

            // Right face (brightness 0.670820)
            int[][] rightPattern =
            {
                new[]
                {
                    2,
                    0,
                },
                new[]
                {
                    3,
                    0,
                },
                new[]
                {
                    0,
                    1,
                },
                new[]
                {
                    1,
                    1,
                },
                new[]
                {
                    2,
                    1,
                },
                new[]
                {
                    3,
                    1,
                },
                new[]
                {
                    0,
                    2,
                },
                new[]
                {
                    1,
                    2,
                },
                new[]
                {
                    2,
                    2,
                },
                new[]
                {
                    3,
                    2,
                },
                new[]
                {
                    0,
                    3,
                },
                new[]
                {
                    1,
                    3,
                },
                new[]
                {
                    2,
                    3,
                },
                new[]
                {
                    3,
                    3,
                },
                new[]
                {
                    0,
                    4,
                },
                new[]
                {
                    1,
                    4,
                },
                new[]
                {
                    2,
                    4,
                },
                new[]
                {
                    3,
                    4,
                },
                new[]
                {
                    0,
                    5,
                },
                new[]
                {
                    1,
                    5,
                },
            };

            DrawCubeFace(pixels, cubeSize, rightPx, tileSize, 0.670820f,
                4, 0, 4 * tileSize,
                -2, 5, 4 * tileSize - 2,
                xOffset, rightPattern);

            Texture2D tex = new(cubeSize, cubeSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, cubeSize, cubeSize),
                new Vector2(0.5f, 0.5f),
                cubeSize);
        }

        /// <summary>
        ///     Draws one face of the isometric cube into the target pixel buffer.
        ///     Uses an affine transform (xu,xv,x1,yu,yv,y1) to map texel (u,v) to
        ///     screen position, then stamps a pattern of pixel offsets per texel.
        ///     Y is flipped from Luanti top-down to Unity bottom-up.
        /// </summary>
        private static void DrawCubeFace(
            Color32[] target, int cubeSize,
            Color32[] facePixels, int size, float brightness,
            int xu, int xv, int x1,
            int yu, int yv, int y1,
            int xOffset, int[][] pattern)
        {
            byte br = (byte)(brightness * 255);

            for (int v = 0; v < size; v++)
            {
                for (int u = 0; u < size; u++)
                {
                    Color32 src = facePixels[v * size + u];

                    if (src.a == 0)
                    {
                        continue;
                    }

                    src.r = (byte)(src.r * br / 255);
                    src.g = (byte)(src.g * br / 255);
                    src.b = (byte)(src.b * br / 255);

                    int sx = xu * u + xv * v + x1;
                    int sy = yu * u + yv * v + y1;

                    for (int p = 0; p < pattern.Length; p++)
                    {
                        int px = sx + pattern[p][0] + xOffset;
                        int py = cubeSize - 1 - (sy + pattern[p][1]);

                        if (px >= 0 && px < cubeSize && py >= 0 && py < cubeSize)
                        {
                            target[py * cubeSize + px] = src;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Reads a texture into a Color32 array at the given size with Point filtering.
        ///     Handles non-readable textures via RenderTexture blit.
        ///     Destroys the temporary Texture2D to avoid leaks.
        /// </summary>
        private static Color32[] ReadPixelsPoint(Texture2D source, int size)
        {
            if (source == null)
            {
                return new Color32[size * size];
            }

            FilterMode original = source.filterMode;
            source.filterMode = FilterMode.Point;

            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D readable = new(size, size, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            source.filterMode = original;

            Color32[] pixels = readable.GetPixels32();
            Object.DestroyImmediate(readable);
            return pixels;
        }
    }
}
