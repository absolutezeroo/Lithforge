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
        // Base textures
        public Texture2D North;
        public Texture2D South;
        public Texture2D East;
        public Texture2D West;
        public Texture2D Up;
        public Texture2D Down;

        // Overlay textures (null = no overlay for this face)
        public Texture2D OverlayNorth;
        public Texture2D OverlaySouth;
        public Texture2D OverlayEast;
        public Texture2D OverlayWest;
        public Texture2D OverlayUp;
        public Texture2D OverlayDown;

        // Per-face base tint type (0=none, 1=grass, 2=foliage, 3=water)
        public byte TintNorth;
        public byte TintSouth;
        public byte TintEast;
        public byte TintWest;
        public byte TintUp;
        public byte TintDown;

        // Per-face overlay tint type (0=none, 1=grass, 2=foliage, 3=water)
        public byte OverlayTintNorth;
        public byte OverlayTintSouth;
        public byte OverlayTintEast;
        public byte OverlayTintWest;
        public byte OverlayTintUp;
        public byte OverlayTintDown;
    }
}
