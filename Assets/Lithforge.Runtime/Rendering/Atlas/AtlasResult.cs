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
        public Texture2DArray TextureArray { get; }

        public IReadOnlyDictionary<Texture2D, int> IndexByTexture { get; }

        public int MissingTextureIndex { get; }

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
