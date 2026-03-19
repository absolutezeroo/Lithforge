using System.Collections.Generic;
using Lithforge.Core.Data;
using UnityEngine;

namespace Lithforge.Runtime.UI.Sprites
{
    /// <summary>
    /// Maps item ResourceIds to Sprite instances for UI display.
    /// Built by ItemSpriteAtlasBuilder during content pipeline.
    /// </summary>
    public sealed class ItemSpriteAtlas
    {
        /// <summary>Lookup of item sprites keyed by ResourceId.</summary>
        private readonly Dictionary<ResourceId, Sprite> _sprites;

        /// <summary>Fallback sprite used when no entry exists for an item.</summary>
        private readonly Sprite _fallback;

        /// <summary>Creates an atlas from the given sprite dictionary and fallback sprite.</summary>
        public ItemSpriteAtlas(Dictionary<ResourceId, Sprite> sprites, Sprite fallback)
        {
            _sprites = sprites;
            _fallback = fallback;
        }

        /// <summary>
        /// Returns the sprite for the given item, or the fallback sprite if not found.
        /// </summary>
        public Sprite Get(ResourceId itemId)
        {
            if (_sprites.TryGetValue(itemId, out Sprite sprite))
            {
                return sprite;
            }

            return _fallback;
        }

        /// <summary>
        /// Returns true if a sprite exists for the given item.
        /// </summary>
        public bool Contains(ResourceId itemId)
        {
            return _sprites.ContainsKey(itemId);
        }

        /// <summary>
        /// Returns the fallback sprite used when no entry exists.
        /// </summary>
        public Sprite GetFallback()
        {
            return _fallback;
        }

        /// <summary>Number of sprites in the atlas.</summary>
        public int Count
        {
            get { return _sprites.Count; }
        }

        /// <summary>
        /// Registers a dynamically composited sprite (e.g., for assembled tools).
        /// </summary>
        public void Register(ResourceId itemId, Sprite sprite)
        {
            _sprites[itemId] = sprite;
        }
    }
}
