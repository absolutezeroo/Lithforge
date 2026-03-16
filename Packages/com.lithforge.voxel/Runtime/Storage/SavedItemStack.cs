namespace Lithforge.Voxel.Storage
{
    public sealed class SavedItemStack
    {
        public int Slot { get; set; }
        public string Ns { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public int Durability { get; set; }

        /// <summary>
        /// Base64-encoded CustomData (e.g. serialized ToolInstance).
        /// Null when item has no custom data.
        /// </summary>
        public string CustomDataBase64 { get; set; }

        public SavedItemStack()
        {
            Slot = 0;
            Ns = "";
            Name = "";
            Count = 0;
            Durability = -1;
            CustomDataBase64 = null;
        }

        public SavedItemStack(int slot, string ns, string name, int count, int durability)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
            CustomDataBase64 = null;
        }

        public SavedItemStack(int slot, string ns, string name, int count, int durability, string customDataBase64)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
            CustomDataBase64 = customDataBase64;
        }
    }
}
