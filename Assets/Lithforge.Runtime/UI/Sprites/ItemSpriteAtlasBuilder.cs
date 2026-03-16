using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Models;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.UI.Sprites
{
    /// <summary>
    /// Builds an ItemSpriteAtlas from item textures and block top-face textures.
    /// Standalone items use textures from Content/Textures/Items/.
    /// Block items use the top face texture from resolvedFaces.
    /// Tool parts use tag-based resolution via ToolPartTextureDatabase.
    /// Uses the same RenderTexture blit pattern as AtlasBuilder for non-readable textures.
    /// </summary>
    public static class ItemSpriteAtlasBuilder
    {
        private const int SpriteSize = 32;

        /// <summary>
        /// Builds the sprite atlas from item entries and resolved block face textures.
        /// </summary>
        public static ItemSpriteAtlas Build(
            List<ItemEntry> itemEntries,
            StateRegistry stateRegistry,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces,
            ToolPartTextureDatabase toolTexDb)
        {
            Dictionary<ResourceId, Sprite> sprites = new Dictionary<ResourceId, Sprite>();

            // Load all item textures from Resources
            Texture2D[] itemTextures = Resources.LoadAll<Texture2D>("Content/Textures/Items");
            Dictionary<string, Texture2D> itemTexturesByName = new Dictionary<string, Texture2D>();

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
                                continue;
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

                StateId baseState = new StateId(stateEntry.BaseStateId);

                if (resolvedFaces.TryGetValue(baseState, out ResolvedFaceTextures2D faces))
                {
                    Texture2D topFace = faces.Up;

                    if (topFace != null)
                    {
                        Sprite sprite = CreateSpriteFromTexture(topFace);

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
                    case "tool_part_head": return ToolPartType.Head;
                    case "tool_part_blade": return ToolPartType.Blade;
                    case "tool_part_handle": return ToolPartType.Handle;
                    case "tool_part_binding": return ToolPartType.Binding;
                    case "tool_part_guard": return ToolPartType.Guard;
                    case "tool_part_point": return ToolPartType.Point;
                    case "tool_part_shaft": return ToolPartType.Shaft;
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

            // Blit through RenderTexture to handle non-readable textures
            Texture2D readable = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            readable.filterMode = FilterMode.Point;

            RenderTexture rt = RenderTexture.GetTemporary(
                SpriteSize, SpriteSize, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, SpriteSize, SpriteSize), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Sprite sprite = Sprite.Create(
                readable,
                new Rect(0, 0, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                SpriteSize);

            return sprite;
        }

        private static Sprite CreateFallbackSprite()
        {
            Texture2D fallbackTex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            fallbackTex.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[SpriteSize * SpriteSize];

            for (int p = 0; p < pixels.Length; p++)
            {
                int px = p % SpriteSize;
                int py = p / SpriteSize;
                bool checker = ((px / 4) + (py / 4)) % 2 == 0;
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
    }
}
