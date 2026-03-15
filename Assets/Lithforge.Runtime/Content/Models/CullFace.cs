namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Specifies which neighboring block face, when opaque, causes this face to be
    /// culled from the mesh. Used per-face on <see cref="ModelFaceEntry"/> to drive
    /// greedy meshing's face-removal optimization.
    /// </summary>
    public enum CullFace
    {
        /// <summary>
        /// Never culled regardless of neighbors. Used for interior faces that should
        /// always render (e.g., cross-model diagonal planes).
        /// </summary>
        None = 0,

        /// <summary>Culled when the block to the north (+Z) is opaque.</summary>
        North = 1,

        /// <summary>Culled when the block to the south (-Z) is opaque.</summary>
        South = 2,

        /// <summary>Culled when the block to the east (+X) is opaque.</summary>
        East = 3,

        /// <summary>Culled when the block to the west (-X) is opaque.</summary>
        West = 4,

        /// <summary>Culled when the block above (+Y) is opaque.</summary>
        Up = 5,

        /// <summary>Culled when the block below (-Y) is opaque.</summary>
        Down = 6,
    }
}
