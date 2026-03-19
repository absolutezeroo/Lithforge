namespace Lithforge.Voxel.Storage
{
    /// <summary>
    /// JSON-serializable entry for <see cref="SavedItemStack"/>.
    /// Each entry represents one data component as a type ID + Base64-encoded binary data.
    /// </summary>
    public sealed class SavedComponentEntry
    {
        /// <summary>Numeric identifier of the component type (e.g. 1 = fuel, 2 = smelt progress).</summary>
        public int TypeId { get; set; }

        /// <summary>Base64-encoded binary payload for the component's serialized state.</summary>
        public string DataBase64 { get; set; }

        /// <summary>Default constructor for JSON deserialization.</summary>
        public SavedComponentEntry()
        {
            TypeId = 0;
            DataBase64 = null;
        }

        /// <summary>Creates a component entry with the given type and Base64-encoded data.</summary>
        public SavedComponentEntry(int typeId, string dataBase64)
        {
            TypeId = typeId;
            DataBase64 = dataBase64;
        }
    }
}
