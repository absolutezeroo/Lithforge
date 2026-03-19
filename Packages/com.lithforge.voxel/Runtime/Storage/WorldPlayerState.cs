namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     JSON-serializable snapshot of a player's position, rotation, time, and inventory.
    ///     Persisted inside <see cref="WorldMetadata"/> as the "player" JSON object.
    /// </summary>
    public sealed class WorldPlayerState
    {
        /// <summary>Player world-space X position.</summary>
        public float PosX { get; set; }

        /// <summary>Player world-space Y position.</summary>
        public float PosY { get; set; }

        /// <summary>Player world-space Z position.</summary>
        public float PosZ { get; set; }

        /// <summary>Camera pitch (rotation around X axis) in degrees.</summary>
        public float RotX { get; set; }

        /// <summary>Camera yaw (rotation around Y axis) in degrees.</summary>
        public float RotY { get; set; }

        /// <summary>In-game time of day (0.0–1.0 normalized day cycle).</summary>
        public double TimeOfDay { get; set; }

        /// <summary>Currently selected hotbar slot index (0-based).</summary>
        public int SelectedSlot { get; set; }

        /// <summary>Serialized inventory slot contents. Empty array when inventory is empty.</summary>
        public SavedItemStack[] Slots { get; set; } = new SavedItemStack[0];
    }
}
