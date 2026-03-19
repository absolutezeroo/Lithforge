using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Rendering.Atlas
{
    /// <summary>
    /// Result of building the texture atlas.
    /// Contains the GPU-side Texture2DArray and a managed lookup
    /// from Texture2D to array slice index.
    /// </summary>
    public sealed class AtlasResult
    {
        /// <summary>GPU-side Texture2DArray containing all block face textures.</summary>
        public Texture2DArray TextureArray { get; }

        /// <summary>Maps source Texture2D references to their array slice index in the atlas.</summary>
        public IReadOnlyDictionary<Texture2D, int> IndexByTexture { get; }

        /// <summary>Array slice index used for the magenta checkerboard missing texture (always 0).</summary>
        public int MissingTextureIndex { get; }

        /// <summary>Creates an atlas result with the given texture array, index lookup, and missing texture index.</summary>
        public AtlasResult(
            Texture2DArray textureArray,
            Dictionary<Texture2D, int> indexByTexture,
            int missingTextureIndex)
        {
            TextureArray = textureArray;
            IndexByTexture = indexByTexture;
            MissingTextureIndex = missingTextureIndex;
        }
    }
}
