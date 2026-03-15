using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Fully resolved per-face Texture2D references for a single block state.
    /// Includes base textures, overlay textures, and per-face tint types.
    ///
    /// Overlay textures (null = no overlay for that face) are alpha-blended
    /// over the base texture by the shader, with independent biome tinting.
    ///
    /// TintType mapping from ModelFaceEntry.TintIndex:
    ///   -1 → 0 (none), 0 → 1 (grass), 1 → 2 (foliage), ≥2 → 3 (water)
    /// </summary>
    public struct ResolvedFaceTextures2D
    {
        /// <summary>Base texture for the north (-Z) face.</summary>
        public Texture2D North;

        /// <summary>Base texture for the south (+Z) face.</summary>
        public Texture2D South;

        /// <summary>Base texture for the east (+X) face.</summary>
        public Texture2D East;

        /// <summary>Base texture for the west (-X) face.</summary>
        public Texture2D West;

        /// <summary>Base texture for the top (+Y) face.</summary>
        public Texture2D Up;

        /// <summary>Base texture for the bottom (-Y) face.</summary>
        public Texture2D Down;

        /// <summary>Overlay texture alpha-blended over the north face, or null if no overlay.</summary>
        public Texture2D OverlayNorth;

        /// <summary>Overlay texture alpha-blended over the south face, or null if no overlay.</summary>
        public Texture2D OverlaySouth;

        /// <summary>Overlay texture alpha-blended over the east face, or null if no overlay.</summary>
        public Texture2D OverlayEast;

        /// <summary>Overlay texture alpha-blended over the west face, or null if no overlay.</summary>
        public Texture2D OverlayWest;

        /// <summary>Overlay texture alpha-blended over the top face, or null if no overlay.</summary>
        public Texture2D OverlayUp;

        /// <summary>Overlay texture alpha-blended over the bottom face, or null if no overlay.</summary>
        public Texture2D OverlayDown;

        /// <summary>Biome tint type for the north face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintNorth;

        /// <summary>Biome tint type for the south face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintSouth;

        /// <summary>Biome tint type for the east face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintEast;

        /// <summary>Biome tint type for the west face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintWest;

        /// <summary>Biome tint type for the top face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintUp;

        /// <summary>Biome tint type for the bottom face base texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte TintDown;

        /// <summary>Biome tint type for the north face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintNorth;

        /// <summary>Biome tint type for the south face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintSouth;

        /// <summary>Biome tint type for the east face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintEast;

        /// <summary>Biome tint type for the west face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintWest;

        /// <summary>Biome tint type for the top face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintUp;

        /// <summary>Biome tint type for the bottom face overlay texture (0=none, 1=grass, 2=foliage, 3=water).</summary>
        public byte OverlayTintDown;
    }
}
