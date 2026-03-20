using Lithforge.Network.Connection;
using Lithforge.Network.Server;

using Unity.Mathematics;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="IChunkStreamingStrategy"/> for testing ServerGameLoop.</summary>
    internal sealed class MockChunkStreamingStrategy : IChunkStreamingStrategy
    {
        /// <summary>Number of times StreamChunk was called.</summary>
        public int StreamCallCount;

        /// <summary>Always returns true (chunk delivered immediately).</summary>
        public bool StreamChunk(PeerInfo peer, int3 coord)
        {
            StreamCallCount++;
            return true;
        }

        /// <summary>Sends unload to peer (no-op).</summary>
        public void SendUnload(PeerInfo peer, int3 coord)
        {
        }
    }
}
