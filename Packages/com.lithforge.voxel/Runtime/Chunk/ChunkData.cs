using System;
using System.Runtime.CompilerServices;
using Lithforge.Voxel.Block;
using Unity.Collections;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Core chunk storage. Owns a NativeArray of StateIds.
    /// Allocated from ChunkPool, returned on unload.
    ///
    /// Thread safety: only ONE writer at a time (generation job or main thread block change).
    /// Multiple readers allowed when chunk is in READY state.
    ///
    /// Index layout: Y-major — y * 1024 + z * 32 + x
    /// Groups horizontal slices for cache-friendly meshing and sunlight propagation.
    /// </summary>
    public struct ChunkData : IDisposable
    {
        public NativeArray<StateId> States;

        public ChunkData(NativeArray<StateId> states)
        {
            States = states;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return (y * ChunkConstants.SizeSquared) + (z * ChunkConstants.Size) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateId GetState(int x, int y, int z)
        {
            return States[GetIndex(x, y, z)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState(int x, int y, int z, StateId state)
        {
            States[GetIndex(x, y, z)] = state;
        }

        public void Dispose()
        {
            if (States.IsCreated)
            {
                States.Dispose();
            }
        }
    }
}
