using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Core.Registry
{
    /// <summary>
    /// Read-only registry of content definitions, frozen after content loading.
    /// </summary>
    public interface IRegistry<T>
    {
        /// <summary>Retrieves the entry for the given identifier, or throws if not found.</summary>
        public T Get(ResourceId id);

        /// <summary>Returns true if the registry contains an entry for the given identifier.</summary>
        public bool Contains(ResourceId id);

        /// <summary>Returns a read-only view of all registered entries.</summary>
        public IReadOnlyDictionary<ResourceId, T> GetAll();

        /// <summary>Total number of entries in the registry.</summary>
        public int Count { get; }
    }
}
