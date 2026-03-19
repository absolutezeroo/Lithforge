using System;
using System.Collections.Generic;

using Lithforge.Core.Data;

namespace Lithforge.Core.Registry
{
    /// <summary>
    ///     Mutable builder for content registries. Used during content loading.
    ///     Call Build() to freeze into an immutable Registry.
    /// </summary>
    public sealed class RegistryBuilder<T>
    {
        /// <summary>Mutable dictionary of entries being accumulated during content loading.</summary>
        private readonly Dictionary<ResourceId, T> _entries = new();

        /// <summary>Whether Build() has been called, preventing further registrations.</summary>
        private bool _frozen;

        /// <summary>Number of entries registered so far.</summary>
        public int Count
        {
            get { return _entries.Count; }
        }

        /// <summary>Registers a new entry, throwing if the registry is frozen or the id is duplicate.</summary>
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

        /// <summary>Returns true if the given identifier has already been registered.</summary>
        public bool Contains(ResourceId id)
        {
            return _entries.ContainsKey(id);
        }

        /// <summary>Freezes the builder and returns an immutable Registry snapshot.</summary>
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
