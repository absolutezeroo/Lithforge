using System.Collections.Generic;

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
    }
}
