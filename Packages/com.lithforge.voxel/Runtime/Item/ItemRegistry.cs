using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    ///     Unified registry for all items: standalone items (tools, stick) and
    ///     auto-generated block items (one per registered block).
    ///     Built after content loading to provide a single lookup point.
    /// </summary>
    public sealed class ItemRegistry
    {
        private readonly Dictionary<ResourceId, ItemEntry> _items = new();

        /// <summary>
        ///     Total number of registered items.
        /// </summary>
        public int Count
        {
            get { return _items.Count; }
        }

        /// <summary>
        ///     Registers all block definitions as block items (max_stack_size=64, IsBlockItem=true).
        ///     Should be called before registering standalone items so explicit definitions can override.
        /// </summary>
        public void RegisterBlockItems(IReadOnlyList<StateRegistryEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];
                ResourceId blockId = entry.Id;

                // Skip air
                if (entry.BaseStateId == 0)
                {
                    continue;
                }

                ItemEntry itemDef = new(blockId);
                itemDef.MaxStackSize = 64;
                itemDef.IsBlockItem = true;
                itemDef.BlockId = blockId;

                _items[blockId] = itemDef;
            }
        }

        /// <summary>
        ///     Registers standalone item definitions (tools, materials).
        ///     Overrides any block item with the same id.
        /// </summary>
        public void RegisterItems(List<ItemEntry> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                _items[items[i].Id] = items[i];
            }
        }

        /// <summary>
        ///     Looks up an item definition by resource id.
        ///     Returns null if not found.
        /// </summary>
        public ItemEntry Get(ResourceId id)
        {
            if (_items.TryGetValue(id, out ItemEntry def))
            {
                return def;
            }

            return null;
        }

        /// <summary>
        ///     Returns true if an item with the given id exists.
        /// </summary>
        public bool Contains(ResourceId id)
        {
            return _items.ContainsKey(id);
        }
    }
}
