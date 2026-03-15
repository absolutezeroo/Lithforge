namespace Lithforge.Voxel.Storage
{
    public sealed class WorldPlayerState
    {
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public double TimeOfDay { get; set; }
        public int SelectedSlot { get; set; }
        public SavedItemStack[] Slots { get; set; }

        public WorldPlayerState()
        {
            Slots = new SavedItemStack[0];
        }
    }
}
