using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries one block command from the server thread to the main thread for
    ///     execution against the real block processor.
    /// </summary>
    internal sealed class BlockCommandRequest
    {
        /// <summary>Discriminator for which block processor method to call.</summary>
        public BlockCommandKind Kind;

        /// <summary>Player issuing the command.</summary>
        public ushort PlayerId;

        /// <summary>Target block world position.</summary>
        public int3 Position;

        /// <summary>Block state for placement commands.</summary>
        public StateId BlockState;

        /// <summary>Face for placement commands.</summary>
        public BlockFace Face;

        /// <summary>Player world position for reach checks.</summary>
        public float3 PlayerPosition;

        /// <summary>Server tick for timing validation.</summary>
        public uint ServerTick;

        /// <summary>Current time for rate limit refills.</summary>
        public float CurrentTime;
    }
}
