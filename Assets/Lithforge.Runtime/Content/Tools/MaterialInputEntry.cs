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
        [Tooltip("Item ID that maps to this material (e.g. 'lithforge:oak_planks')")]
        public string itemId;

        [Tooltip("Material units this item provides. E.g. log=4, planks=1.")]
        [Min(1)] public int value;

        [Tooltip("Number of items needed to get 'value' units. E.g. sticks: needed=2, value=1.")]
        [Min(1)] public int needed;

        [Tooltip("Item returned as leftover when value > cost. E.g. 'lithforge:oak_planks' " +
                 "when a log has excess. Empty = no leftover.")]
        public string leftoverItemId;
    }
}
