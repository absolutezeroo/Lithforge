using System;
using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Core.Registry
{
    /// <summary>
    /// Immutable, frozen registry of content definitions.
    /// Created by RegistryBuilder.Build() after content loading completes.
    /// </summary>
    public sealed class Registry<T> : IRegistry<T>
    {
        private readonly Dictionary<ResourceId, T> _entries;

        internal Registry(Dictionary<ResourceId, T> entries)
        {
            _entries = entries;
        }

        public T Get(ResourceId id)
        {
            if (!_entries.TryGetValue(id, out T value))
            {
                throw new KeyNotFoundException($"No entry found for '{id}'.");
            }

            return value;
        }

        public bool Contains(ResourceId id)
        {
            return _entries.ContainsKey(id);
        }

        public IReadOnlyDictionary<ResourceId, T> GetAll()
        {
            return _entries;
        }

        public int Count
        {
            get { return _entries.Count; }
        }
    }
}
