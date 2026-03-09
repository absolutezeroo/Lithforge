using System;
using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Core.Registry
{
    /// <summary>
    /// Mutable builder for content registries. Used during content loading.
    /// Call Build() to freeze into an immutable Registry.
    /// </summary>
    public sealed class RegistryBuilder<T>
    {
        private readonly Dictionary<ResourceId, T> _entries = new Dictionary<ResourceId, T>();
        private bool _frozen;

        public void Register(ResourceId id, T value)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    $"Cannot register '{id}' — registry has been frozen.");
            }

            if (_entries.ContainsKey(id))
            {
                throw new ArgumentException(
                    $"Duplicate registration for '{id}'.");
            }

            _entries[id] = value;
        }

        public bool Contains(ResourceId id)
        {
            return _entries.ContainsKey(id);
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public Registry<T> Build()
        {
            if (_frozen)
            {
                throw new InvalidOperationException("Registry has already been built.");
            }

            _frozen = true;

            return new Registry<T>(new Dictionary<ResourceId, T>(_entries));
        }
    }
}
