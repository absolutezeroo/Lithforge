using System;
using System.Collections.Generic;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     JSON-serializable representation of an inventory slot for world save files.
    ///     Contains the item identity (namespace + name), stack count, durability,
    ///     and optional component data for block entities.
    /// </summary>
    public sealed class SavedItemStack
    {
        /// <summary>Default constructor for JSON deserialization.</summary>
        public SavedItemStack()
        {
            Slot = 0;
            Ns = "";
            Name = "";
            Count = 0;
            Durability = -1;
            Components = null;
            CustomDataBase64 = null;
        }

        /// <summary>Creates a saved item stack with identity and count (no component data).</summary>
        public SavedItemStack(int slot, string ns, string name, int count, int durability)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
            Components = null;
            CustomDataBase64 = null;
        }

        /// <summary>Creates a saved item stack with typed component data (v2 format).</summary>
        public SavedItemStack(int slot, string ns, string name, int count, int durability,
            List<SavedComponentEntry> components)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
            Components = components;
            CustomDataBase64 = null;
        }

        /// <summary>
        ///     Legacy constructor for backward compat reads.
        /// </summary>
        [Obsolete("Use the constructor with List<SavedComponentEntry> instead.")]
        public SavedItemStack(int slot, string ns, string name, int count, int durability,
            string customDataBase64)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
            Components = null;
            CustomDataBase64 = customDataBase64;
        }
        /// <summary>Inventory slot index this stack occupies (0-based).</summary>
        public int Slot { get; set; }

        /// <summary>ResourceId namespace (e.g. "lithforge").</summary>
        public string Ns { get; set; }

        /// <summary>ResourceId name (e.g. "stone_pickaxe").</summary>
        public string Name { get; set; }

        /// <summary>Number of items in the stack.</summary>
        public int Count { get; set; }

        /// <summary>Remaining durability (-1 = no durability / infinite).</summary>
        public int Durability { get; set; }

        /// <summary>
        ///     Typed component entries (v2 JSON format).
        ///     Each entry is a type ID + Base64-encoded binary data.
        /// </summary>
        public List<SavedComponentEntry> Components { get; set; }

        /// <summary>
        ///     Legacy Base64-encoded CustomData. Kept for backward compat reads.
        /// </summary>
        [Obsolete("Use Components instead. Retained for legacy save migration.")]
        public string CustomDataBase64 { get; set; }

        /// <summary>
        ///     Returns true if this stack has component data (new format) or legacy custom data.
        /// </summary>
        public bool HasData
        {
            get
            {
                return Components is
                       {
                           Count: > 0,
                       }
                       || !string.IsNullOrEmpty(CustomDataBase64);
            }
        }
    }
}
