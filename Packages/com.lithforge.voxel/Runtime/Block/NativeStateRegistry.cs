using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Read-only Burst-compatible view of the state registry.
    /// Created once at content load freeze via StateRegistry.BakeNative().
    /// Disposed at shutdown. Passed as [ReadOnly] input to all Burst jobs
    /// that need block info.
    ///
    /// Owner: ContentManager (or whoever calls StateRegistry.BakeNative)
    /// Allocator: Persistent
    /// Dispose: at shutdown
    /// </summary>
    public struct NativeStateRegistry : IDisposable
    {
        /// <summary>Flat array of all registered block states, indexed by StateId.Value.</summary>
        [ReadOnly] public NativeArray<BlockStateCompact> States;

        /// <summary>Wraps an existing NativeArray as a state registry.</summary>
        public NativeStateRegistry(NativeArray<BlockStateCompact> states)
        {
            States = states;
        }

        /// <summary>Total number of registered block states.</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return States.Length; }
        }

        /// <summary>Returns the compact block state for the given StateId.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockStateCompact GetState(StateId id)
        {
            return States[id.Value];
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
