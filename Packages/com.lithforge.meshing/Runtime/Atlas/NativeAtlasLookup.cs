using System;
using System.Runtime.CompilerServices;
using Lithforge.Voxel.Block;
using Unity.Collections;

namespace Lithforge.Meshing.Atlas
{
    /// <summary>
    /// Burst-accessible texture atlas lookup indexed by StateId.
    /// Owner: LithforgeBootstrap. Allocator: Persistent. Dispose: OnDestroy.
    /// </summary>
    public struct NativeAtlasLookup : IDisposable
    {
        [ReadOnly] public NativeArray<AtlasEntry> Entries;

        public int TextureCount;

        public NativeAtlasLookup(NativeArray<AtlasEntry> entries, int textureCount)
        {
            Entries = entries;
            TextureCount = textureCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AtlasEntry GetEntry(StateId id)
        {
            return Entries[id.Value];
        }

        public void Dispose()
        {
            if (Entries.IsCreated)
            {
                Entries.Dispose();
            }
        }
    }
}
