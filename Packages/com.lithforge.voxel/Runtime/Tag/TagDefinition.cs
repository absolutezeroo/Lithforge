using System.Collections.Generic;

using Lithforge.Core.Data;

namespace Lithforge.Voxel.Tag
{
    /// <summary>
    ///     Data-driven tag definition parsed from data/{ns}/tags/{category}/{id}.json.
    ///     Tags group blocks or items by shared behavior (e.g. mineable_pickaxe, logs).
    /// </summary>
    public sealed class TagDefinition
    {
        /// <summary>Creates a tag definition with the given resource id and empty value list.</summary>
        public TagDefinition(ResourceId id)
        {
            Id = id;
            Replace = false;
            Values = new List<string>();
        }
        /// <summary>Unique identifier for this tag (e.g. "lithforge:mineable_pickaxe").</summary>
        public ResourceId Id { get; }

        /// <summary>When true, existing members for this tag are cleared before adding new values.</summary>
        public bool Replace { get; set; }

        /// <summary>List of resource id strings that belong to this tag.</summary>
        public List<string> Values { get; set; }
    }
}
