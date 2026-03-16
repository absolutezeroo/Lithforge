namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Canonical six-face identifier for block interaction (placement face, hit face).
    /// Byte-backed for blittable struct packing. Values match the face direction
    /// convention used by ExtractAllBordersJob and RaycastHit.Normal.
    /// </summary>
    public enum BlockFace : byte
    {
        /// <summary>+X face (East).</summary>
        East = 0,

        /// <summary>-X face (West).</summary>
        West = 1,

        /// <summary>+Y face (Up).</summary>
        Up = 2,

        /// <summary>-Y face (Down).</summary>
        Down = 3,

        /// <summary>+Z face (North).</summary>
        North = 4,

        /// <summary>-Z face (South).</summary>
        South = 5,
    }
}
