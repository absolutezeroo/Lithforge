using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Tag
{
    /// <summary>
    /// Registry that maps tag ids to their member sets, and provides
    /// reverse lookups (resource id → tags it belongs to).
    /// Built from TagDefinitions after content loading.
    /// </summary>
    public sealed class TagRegistry
    {
        private static readonly ResourceId[] _emptySet = System.Array.Empty<ResourceId>();

        private readonly Dictionary<ResourceId, HashSet<ResourceId>> _tagToMembers;
        private readonly Dictionary<ResourceId, HashSet<ResourceId>> _memberToTags;

        public TagRegistry()
        {
            _tagToMembers = new Dictionary<ResourceId, HashSet<ResourceId>>();
            _memberToTags = new Dictionary<ResourceId, HashSet<ResourceId>>();
        }

        /// <summary>
        /// Registers a tag definition, adding all its values to the lookup tables.
        /// If replace is true, existing members for this tag are cleared first.
        /// </summary>
        public void Register(TagDefinition definition)
        {
            ResourceId tagId = definition.Id;

            if (definition.Replace || !_tagToMembers.ContainsKey(tagId))
            {
                _tagToMembers[tagId] = new HashSet<ResourceId>();
            }

            HashSet<ResourceId> members = _tagToMembers[tagId];

            for (int i = 0; i < definition.Values.Count; i++)
            {
                string value = definition.Values[i];

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                ResourceId memberId = ResourceId.Parse(value);
                members.Add(memberId);

                if (!_memberToTags.TryGetValue(memberId, out HashSet<ResourceId> tags))
                {
                    tags = new HashSet<ResourceId>();
                    _memberToTags[memberId] = tags;
                }

                tags.Add(tagId);
            }
        }

        /// <summary>
        /// Returns true if the given resource is a member of the specified tag.
        /// </summary>
        public bool HasTag(ResourceId memberId, ResourceId tagId)
        {
            if (_memberToTags.TryGetValue(memberId, out HashSet<ResourceId> tags))
            {
                return tags.Contains(tagId);
            }

            return false;
        }

        /// <summary>
        /// Returns all members of a tag, or an empty set if the tag doesn't exist.
        /// </summary>
        public IReadOnlyCollection<ResourceId> GetMembers(ResourceId tagId)
        {
            if (_tagToMembers.TryGetValue(tagId, out HashSet<ResourceId> members))
            {
                return members;
            }

            return _emptySet;
        }

        /// <summary>
        /// Returns all tags that a resource belongs to.
        /// </summary>
        public IReadOnlyCollection<ResourceId> GetTags(ResourceId memberId)
        {
            if (_memberToTags.TryGetValue(memberId, out HashSet<ResourceId> tags))
            {
                return tags;
            }

            return _emptySet;
        }

        /// <summary>
        /// Returns the total number of registered tags.
        /// </summary>
        public int TagCount
        {
            get { return _tagToMembers.Count; }
        }
    }
}
