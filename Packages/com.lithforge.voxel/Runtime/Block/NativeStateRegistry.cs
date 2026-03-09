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
        [ReadOnly] public NativeArray<BlockStateCompact> States;

        public NativeStateRegistry(NativeArray<BlockStateCompact> states)
        {
            States = states;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return States.Length; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockStateCompact GetState(StateId id)
        {
            return States[id.Value];
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
