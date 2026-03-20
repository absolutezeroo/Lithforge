using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Network.Chunk;

using Unity.Mathematics;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Minimal mock of <see cref="IDirtyChangeSource"/> for testing ServerGameLoop.</summary>
    internal sealed class MockDirtyChangeSource : IDirtyChangeSource
    {
        /// <summary>Pre-configured dirty changes to return on the next FlushAll call.</summary>
        public Dictionary<int3, List<BlockChangeEntry>> NextFlush = new();

        /// <summary>Number of times FlushAll was called.</summary>
        public int FlushCallCount;

        /// <summary>Returns the configured dirty changes and increments the call counter.</summary>
        public Dictionary<int3, List<BlockChangeEntry>> FlushAll()
        {
            FlushCallCount++;
            return NextFlush;
        }
    }
}
