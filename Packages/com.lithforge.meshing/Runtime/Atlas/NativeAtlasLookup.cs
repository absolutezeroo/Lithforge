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
        /// <summary>Read-only array of per-state atlas entries indexed by StateId.Value.</summary>
        [ReadOnly] public NativeArray<AtlasEntry> Entries;

        /// <summary>Total number of textures in the atlas Texture2DArray.</summary>
        public int TextureCount;

        /// <summary>Creates a lookup from an existing entries array and texture count.</summary>
        public NativeAtlasLookup(NativeArray<AtlasEntry> entries, int textureCount)
        {
            Entries = entries;
            TextureCount = textureCount;
        }

        /// <summary>Returns the atlas entry for the given block state.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AtlasEntry GetEntry(StateId id)
        {
            return Entries[id.Value];
        }

        /// <summary>Disposes the underlying NativeArray if created.</summary>
        public void Dispose()
        {
            if (Entries.IsCreated)
            {
                Entries.Dispose();
            }
        }
    }
}
