using System.Collections.Generic;
using Lithforge.Core.Data;
using UnityEngine;

namespace Lithforge.Runtime.Rendering.Atlas
{
    /// <summary>
    /// Result of building the texture atlas.
    /// Contains the GPU-side Texture2DArray and a managed lookup
    /// from texture ResourceId to array slice index.
    /// </summary>
    public sealed class AtlasResult
    {
        public Texture2DArray TextureArray { get; }

        public IReadOnlyDictionary<ResourceId, int> IndexByTexture { get; }

        public int MissingTextureIndex { get; }

        public AtlasResult(
            Texture2DArray textureArray,
            Dictionary<ResourceId, int> indexByTexture,
            int missingTextureIndex)
        {
            TextureArray = textureArray;
            IndexByTexture = indexByTexture;
            MissingTextureIndex = missingTextureIndex;
        }
    }
}
