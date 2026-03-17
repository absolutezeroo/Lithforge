namespace Lithforge.Voxel.Storage
{
    /// <summary>
    /// JSON-serializable entry for <see cref="SavedItemStack"/>.
    /// Each entry represents one data component as a type ID + Base64-encoded binary data.
    /// </summary>
    public sealed class SavedComponentEntry
    {
        public int TypeId { get; set; }

        public string DataBase64 { get; set; }

        public SavedComponentEntry()
        {
            TypeId = 0;
            DataBase64 = null;
        }

        public SavedComponentEntry(int typeId, string dataBase64)
        {
            TypeId = typeId;
            DataBase64 = dataBase64;
        }
    }
}
