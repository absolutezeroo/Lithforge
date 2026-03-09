using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Tag
{
    /// <summary>
    /// Data-driven tag definition parsed from data/{ns}/tags/{category}/{id}.json.
    /// Tags group blocks or items by shared behavior (e.g. mineable_pickaxe, logs).
    /// </summary>
    public sealed class TagDefinition
    {
        public ResourceId Id { get; }
        public bool Replace { get; set; }
        public List<string> Values { get; set; }

        public TagDefinition(ResourceId id)
        {
            Id = id;
            Replace = false;
            Values = new List<string>();
        }
    }
}
