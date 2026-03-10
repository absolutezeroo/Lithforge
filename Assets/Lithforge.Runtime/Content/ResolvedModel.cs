using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Runtime.Content
{
    /// <summary>
    /// Result of resolving a BlockModel parent chain.
    /// Contains both the resolved face textures and the merged element list.
    /// </summary>
    public sealed class ResolvedModel
    {
        public ResolvedFaceTextures Textures { get; set; }
        public List<ModelElement> Elements { get; set; }
    }
}
