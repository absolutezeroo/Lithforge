namespace Lithforge.Voxel.Storage
{
    public sealed class SavedItemStack
    {
        public int Slot { get; set; }
        public string Ns { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public int Durability { get; set; }

        public SavedItemStack()
        {
            Slot = 0;
            Ns = "";
            Name = "";
            Count = 0;
            Durability = -1;
        }

        public SavedItemStack(int slot, string ns, string name, int count, int durability)
        {
            Slot = slot;
            Ns = ns;
            Name = name;
            Count = count;
            Durability = durability;
        }
    }
}
