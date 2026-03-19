using System;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    /// Immutable snapshot of all connected players' chunk coordinates,
    /// written by the server thread each tick and read by the main thread each frame.
    /// The reference is swapped atomically via the volatile field on
    /// <see cref="ServerThreadBridge" />. Stale reads are acceptable because
    /// chunk loading is eventually consistent.
    /// </summary>
    public sealed class PlayerChunkSnapshot
    {
        /// <summary>Singleton empty snapshot for use when no players are connected.</summary>
        public static readonly PlayerChunkSnapshot Empty = new(Array.Empty<int3>(), 0);

        /// <summary>Chunk coordinates for each connected player. Length may exceed Count (pooled).</summary>
        public readonly int3[] Coords;

        /// <summary>Number of valid entries in <see cref="Coords" />.</summary>
        public readonly int Count;

        /// <summary>Creates a snapshot from the given coordinate array and valid entry count.</summary>
        public PlayerChunkSnapshot(int3[] coords, int count)
        {
            Coords = coords;
            Count = count;
        }
    }
}
