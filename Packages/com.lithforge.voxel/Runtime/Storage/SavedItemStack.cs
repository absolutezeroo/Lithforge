using System;
using System.Collections.Generic;

namespace Lithforge.Voxel.Storage
{
    public sealed class SavedItemStack
    {
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
        public int Slot { get; set; }

        public string Ns { get; set; }

        public string Name { get; set; }

        public int Count { get; set; }

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
