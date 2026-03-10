using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Fully resolved per-face Texture2D references for a single block state.
    /// Tier 3 replacement for the Tier 1 ResolvedFaceTextures (which used ResourceId).
    /// </summary>
    public struct ResolvedFaceTextures2D
    {
        public Texture2D North;
        public Texture2D South;
        public Texture2D East;
        public Texture2D West;
        public Texture2D Up;
        public Texture2D Down;
    }
}
