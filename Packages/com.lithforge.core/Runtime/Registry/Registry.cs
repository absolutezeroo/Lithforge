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
        /// <summary>Backing dictionary mapping identifiers to content definitions.</summary>
        private readonly Dictionary<ResourceId, T> _entries;

        /// <summary>Creates a frozen registry from a pre-built dictionary of entries.</summary>
        internal Registry(Dictionary<ResourceId, T> entries)
        {
            _entries = entries;
        }

        /// <summary>Retrieves the entry for the given identifier, or throws KeyNotFoundException.</summary>
        public T Get(ResourceId id)
        {
            if (!_entries.TryGetValue(id, out T value))
            {
                throw new KeyNotFoundException($"No entry found for '{id}'.");
            }

            return value;
        }

        /// <summary>Returns true if an entry exists for the given identifier.</summary>
        public bool Contains(ResourceId id)
        {
            return _entries.ContainsKey(id);
        }

        /// <summary>Returns a read-only view of all registered entries.</summary>
        public IReadOnlyDictionary<ResourceId, T> GetAll()
        {
            return _entries;
        }

        /// <summary>Total number of entries in the registry.</summary>
        public int Count
        {
            get { return _entries.Count; }
        }
    }
}
