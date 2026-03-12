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
    /// Uses the same RenderTexture blit pattern as AtlasBuilder for non-readable textures.
    /// </summary>
    public static class ItemSpriteAtlasBuilder
    {
        private const int _spriteSize = 32;

        /// <summary>
        /// Builds the sprite atlas from item entries and resolved block face textures.
        /// </summary>
        public static ItemSpriteAtlas Build(
            List<ItemEntry> itemEntries,
            StateRegistry stateRegistry,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces)
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

        private static Sprite CreateSpriteFromTexture(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            // Blit through RenderTexture to handle non-readable textures
            Texture2D readable = new Texture2D(_spriteSize, _spriteSize, TextureFormat.RGBA32, false);
            readable.filterMode = FilterMode.Point;

            RenderTexture rt = RenderTexture.GetTemporary(
                _spriteSize, _spriteSize, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, _spriteSize, _spriteSize), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Sprite sprite = Sprite.Create(
                readable,
                new Rect(0, 0, _spriteSize, _spriteSize),
                new Vector2(0.5f, 0.5f),
                _spriteSize);

            return sprite;
        }

        private static Sprite CreateFallbackSprite()
        {
            Texture2D fallbackTex = new Texture2D(_spriteSize, _spriteSize, TextureFormat.RGBA32, false);
            fallbackTex.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[_spriteSize * _spriteSize];

            for (int p = 0; p < pixels.Length; p++)
            {
                int px = p % _spriteSize;
                int py = p / _spriteSize;
                bool checker = ((px / 4) + (py / 4)) % 2 == 0;
                pixels[p] = checker
                    ? new Color32(255, 0, 255, 255)
                    : new Color32(0, 0, 0, 255);
            }

            fallbackTex.SetPixels32(pixels);
            fallbackTex.Apply();

            return Sprite.Create(
                fallbackTex,
                new Rect(0, 0, _spriteSize, _spriteSize),
                new Vector2(0.5f, 0.5f),
                _spriteSize);
        }
    }
}
