using System;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Pairs a <see cref="Lithforge.Core.ResourceId"/> (split into namespace + name) with a stack count,
    /// used by <see cref="GameplaySettings"/> to define what a player receives on first spawn.
    /// </summary>
    [Serializable]
    public struct StartingItemEntry
    {
        /// <summary>Registry namespace portion of the item's ResourceId (e.g. "lithforge").</summary>
        [Tooltip("Item namespace (e.g. lithforge)")]
        public string itemNamespace;

        /// <summary>Registry name portion of the item's ResourceId (e.g. "cobblestone").</summary>
        [Tooltip("Item name (e.g. cobblestone)")]
        public string itemName;

        /// <summary>How many of this item to place in the inventory.</summary>
        [Min(1)]
        [Tooltip("Number of items to grant")]
        public int count;
    }
}
