using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Result of resolving a BlockModel parent chain.
    /// Contains both the resolved face textures and the merged element list.
    /// </summary>
    public sealed class ResolvedModel
    {
        public ResolvedFaceTextures2D Textures { get; set; }

        public List<ModelElement> Elements { get; set; }

        /// <summary>
        /// The fully resolved texture variable dictionary (variable name → Texture2D).
        /// Built during parent chain walk; exposed to avoid redundant re-resolution.
        /// </summary>
        public Dictionary<string, Texture2D> ResolvedTextureDictionary { get; set; }
    }
}
