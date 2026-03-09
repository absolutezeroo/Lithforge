using System.Collections.Generic;

namespace Lithforge.Core.Data
{
    /// <summary>
    /// Managed representation of a parsed blockstate JSON file.
    /// Maps property value combinations (variant keys) to model variants.
    /// Example variant keys: "" (no properties), "axis=y", "facing=north,lit=false"
    /// </summary>
    public sealed class BlockstateDefinition
    {
        public ResourceId Id { get; }

        public IReadOnlyDictionary<string, BlockstateVariant> Variants { get; }

        public BlockstateDefinition(ResourceId id, Dictionary<string, BlockstateVariant> variants)
        {
            Id = id;
            Variants = variants;
        }
    }
}
