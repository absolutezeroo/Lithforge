using System.Collections.Generic;

namespace Lithforge.Core.Data
{
    /// <summary>
    /// Managed representation of a parsed block model JSON file.
    /// Contains a parent reference and texture variable mappings.
    /// Texture values may be "#variable" references or direct "namespace:path" ResourceIds.
    /// </summary>
    public sealed class BlockModel
    {
        public ResourceId Id { get; }

        public string Parent { get; set; }

        public Dictionary<string, string> Textures { get; }

        public BlockModel(ResourceId id)
        {
            Id = id;
            Textures = new Dictionary<string, string>();
        }
    }
}
