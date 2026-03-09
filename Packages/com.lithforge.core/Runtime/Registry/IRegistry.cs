using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Core.Registry
{
    /// <summary>
    /// Read-only registry of content definitions, frozen after content loading.
    /// </summary>
    public interface IRegistry<T>
    {
        public T Get(ResourceId id);

        public bool Contains(ResourceId id);

        public IReadOnlyDictionary<ResourceId, T> GetAll();

        public int Count { get; }
    }
}
