using System;
using System.Collections.Generic;

namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    /// Registry of block entity types. Maps type ID strings to factories.
    /// Supports freeze to prevent late registration.
    /// </summary>
    public sealed class BlockEntityRegistry
    {
        private readonly Dictionary<string, BlockEntityType> _types = new();
        private bool _frozen;

        /// <summary>
        /// Registers a block entity type. Must be called before Freeze().
        /// </summary>
        public void Register(BlockEntityType type)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    $"Cannot register block entity type '{type.TypeId}' — registry is frozen.");
            }

            if (_types.ContainsKey(type.TypeId))
            {
                throw new InvalidOperationException(
                    $"Block entity type '{type.TypeId}' is already registered.");
            }

            _types[type.TypeId] = type;
        }

        /// <summary>
        /// Freezes the registry, preventing further registration.
        /// </summary>
        public void Freeze()
        {
            _frozen = true;
        }

        /// <summary>
        /// Creates a new block entity instance by type ID.
        /// Returns null if the type is not registered.
        /// </summary>
        public IBlockEntity CreateEntity(string typeId)
        {
            if (_types.TryGetValue(typeId, out BlockEntityType type))
            {
                return type.Factory.Create();
            }

            return null;
        }

        /// <summary>
        /// Returns true if a type with the given ID is registered.
        /// </summary>
        public bool HasType(string typeId)
        {
            return _types.ContainsKey(typeId);
        }

        public int Count
        {
            get { return _types.Count; }
        }
    }
}
