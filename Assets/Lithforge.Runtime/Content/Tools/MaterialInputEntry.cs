using System;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Serializable entry mapping an item to a material with a value/needed ratio.
    /// Used on ToolMaterialDefinition for Part Builder material inputs.
    /// </summary>
    [Serializable]
    public struct MaterialInputEntry
    {
        /// <summary>ResourceId of the item that provides this material (e.g. "lithforge:oak_planks").</summary>
        [Tooltip("Item ID that maps to this material (e.g. 'lithforge:oak_planks')")]
        public string itemId;

        /// <summary>Material units this item provides (e.g. log=4, planks=1).</summary>
        [Tooltip("Material units this item provides. E.g. log=4, planks=1.")]
        [Min(1)] public int value;

        /// <summary>Number of items consumed to yield the specified value units.</summary>
        [Tooltip("Number of items needed to get 'value' units. E.g. sticks: needed=2, value=1.")]
        [Min(1)] public int needed;

        /// <summary>Item returned as leftover when value exceeds cost, or empty for no leftover.</summary>
        [Tooltip("Item returned as leftover when value > cost. E.g. 'lithforge:oak_planks' " +
                 "when a log has excess. Empty = no leftover.")]
        public string leftoverItemId;
    }
}
