using System.Collections.Generic;

using Lithforge.Network.Server;

using Unity.Mathematics;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="IServerChunkProvider"/> for testing ServerGameLoop.</summary>
    internal sealed class MockServerChunkProvider : IServerChunkProvider
    {
        /// <summary>Set of chunk coordinates considered ready.</summary>
        public HashSet<int3> ReadyChunks = new();

        /// <summary>Fallback Y coordinate returned by FindSafeSpawnY.</summary>
        public int FallbackSpawnY = 64;

        /// <summary>Returns true if the coordinate is in the ReadyChunks set.</summary>
        public bool IsChunkReady(int3 coord)
        {
            return ReadyChunks.Contains(coord);
        }

        /// <summary>Returns a minimal dummy byte array.</summary>
        public byte[] SerializeChunk(int3 coord)
        {
            return new byte[] { 0x4C, 0x46, 0x43, 0x4B, 1 };
        }

        /// <summary>Returns the configured fallback Y.</summary>
        public int FindSafeSpawnY(int worldX, int worldZ, int chunkYMin, int chunkYMax, int fallbackY)
        {
            return FallbackSpawnY;
        }

        /// <summary>Returns true if the coordinate is in the ReadyChunks set.</summary>
        public bool IsChunkGenerated(int3 coord)
        {
            return ReadyChunks.Contains(coord);
        }

        /// <summary>Returns false (no chunks are all-air in mock).</summary>
        public bool IsChunkAllAir(int3 coord)
        {
            return false;
        }

        /// <summary>Returns 0 as the network version.</summary>
        public int GetChunkNetworkVersion(int3 coord)
        {
            return ReadyChunks.Contains(coord) ? 0 : -1;
        }
    }
}
