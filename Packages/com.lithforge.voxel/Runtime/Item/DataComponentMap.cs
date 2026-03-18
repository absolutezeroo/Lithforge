using System.Collections;
using System.Collections.Generic;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Per-stack container for typed data components. Null on most items (zero allocation).
    /// Keyed by int type ID.
    /// </summary>
    public sealed class DataComponentMap : IEnumerable<KeyValuePair<int, IDataComponent>>
    {
        private readonly Dictionary<int, IDataComponent> _components = new();

        /// <summary>
        /// Gets a typed component by type ID, or null if not present.
        /// </summary>
        public T Get<T>(int typeId) where T : class, IDataComponent
        {
            if (_components.TryGetValue(typeId, out IDataComponent component))
            {
                return component as T;
            }

            return null;
        }

        /// <summary>
        /// Adds or replaces a component by type ID.
        /// </summary>
        public void Set(int typeId, IDataComponent component)
        {
            _components[typeId] = component;
        }

        /// <summary>
        /// Returns true if a component with the given type ID exists.
        /// </summary>
        public bool Has(int typeId)
        {
            return _components.ContainsKey(typeId);
        }

        /// <summary>
        /// Removes a component by type ID. Returns true if it was present.
        /// </summary>
        public bool Remove(int typeId)
        {
            return _components.Remove(typeId);
        }

        /// <summary>
        /// Returns true if the map is empty (no components).
        /// </summary>
        public bool IsEmpty
        {
            get { return _components.Count == 0; }
        }

        /// <summary>
        /// Returns the number of components in the map.
        /// </summary>
        public int Count
        {
            get { return _components.Count; }
        }

        /// <summary>
        /// Structural equality: same keys, and each component's Equals returns true.
        /// </summary>
        public bool ContentEquals(DataComponentMap other)
        {
            if (other == null)
            {
                return _components.Count == 0;
            }

            if (_components.Count != other._components.Count)
            {
                return false;
            }

            foreach (KeyValuePair<int, IDataComponent> kvp in _components)
            {
                if (!other._components.TryGetValue(kvp.Key, out IDataComponent otherComponent))
                {
                    return false;
                }

                if (!Equals(kvp.Value, otherComponent))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerator<KeyValuePair<int, IDataComponent>> GetEnumerator()
        {
            return _components.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
