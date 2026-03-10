using System;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [Serializable]
    public struct StartingItemEntry
    {
        [Tooltip("Item namespace (e.g. lithforge)")]
        public string Namespace;

        [Tooltip("Item name (e.g. cobblestone)")]
        public string Name;

        [Min(1)]
        [Tooltip("Number of items to grant")]
        public int Count;
    }
}
