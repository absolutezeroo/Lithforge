using System;
using System.Runtime.CompilerServices;

using Lithforge.Voxel.Block;

using Unity.Collections;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Core chunk storage. Owns a NativeArray of StateIds.
    ///     Allocated from ChunkPool, returned on unloading.
    ///     Thread safety: only ONE writer at a time (generation job or main thread block change).
    ///     Multiple readers are allowed when a chunk is in the READY state.
    ///     Index layout: Y-major — y * 1024 + z * 32 + x
    ///     Groups horizontal slices for cache-friendly meshing and sunlight propagation.
    /// </summary>
    public struct ChunkData : IDisposable
    {
        /// <summary>Flat voxel array (Volume=32768) indexed Y-major: y*1024 + z*32 + x.</summary>
        public NativeArray<StateId> States;

        /// <summary>Wraps an existing NativeArray as chunk voxel data.</summary>
        public ChunkData(NativeArray<StateId> states)
        {
            States = states;
        }

        /// <summary>Computes the flat Y-major index for local coordinates (x, y, z).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return y * ChunkConstants.SizeSquared + z * ChunkConstants.Size + x;
        }

        /// <summary>Returns the block state at (x, y, z).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateId GetState(int x, int y, int z)
        {
            return States[GetIndex(x, y, z)];
        }

        /// <summary>Sets the block state at (x, y, z).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState(int x, int y, int z, StateId state)
        {
            States[GetIndex(x, y, z)] = state;
        }

        /// <summary>Disposes the underlying NativeArray if created.</summary>
        public void Dispose()
        {
            if (States.IsCreated)
            {
                States.Dispose();
            }
        }
    }
}
