using System;
using System.Collections.Generic;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Grows GPU GraphicsBuffers via GPU-to-GPU compute copy, eliminating CPU
    ///     re-upload stalls on resize. Old buffers are retired after
    ///     <see cref="RetireFrameDelay" /> frames to ensure the GPU has finished
    ///     reading from them before disposal.
    ///     All buffers passed to <see cref="Resize" /> must have
    ///     <see cref="GraphicsBuffer.Target.Raw" /> in their target flags so the
    ///     ByteAddressBuffer compute bindings are legal.
    ///     Owner: LithforgeBootstrap (one shared instance per session).
    ///     <see cref="Tick" /> must be called once per frame from GameLoop.Update
    ///     before any grow paths.
    ///     Lifetime: game session.
    /// </summary>
    public sealed class GpuBufferResizer : IDisposable
    {
        /// <summary>
        ///     Number of frames an old buffer is held before disposal.
        ///     3 frames is conservative for GPU command buffer depth on PC/console.
        /// </summary>
        public const int RetireFrameDelay = 3;

        /// <summary>Shader property ID for the source ByteAddressBuffer in the copy kernel.</summary>
        private static readonly int s_srcId = Shader.PropertyToID("_Src");

        /// <summary>Shader property ID for the destination ByteAddressBuffer in copy/zero kernels.</summary>
        private static readonly int s_dstId = Shader.PropertyToID("_Dst");

        /// <summary>Shader property ID for the number of bytes to copy.</summary>
        private static readonly int s_byteCountId = Shader.PropertyToID("_ByteCount");

        /// <summary>Shader property ID for the byte offset at which zeroing begins.</summary>
        private static readonly int s_zeroStartId = Shader.PropertyToID("_ZeroStart");

        /// <summary>Shader property ID for the number of bytes to zero in the tail region.</summary>
        private static readonly int s_zeroByteCountId = Shader.PropertyToID("_ZeroByteCount");

        /// <summary>Compute shader kernel index for the GPU-to-GPU byte copy operation.</summary>
        private readonly int _copyKernel;

        /// <summary>Queue of old buffers awaiting deferred disposal after the GPU finishes reading.</summary>
        private readonly List<DeferredDisposal> _disposalQueue = new(8);

        /// <summary>Compute shader used for GPU buffer copy and zero operations.</summary>
        private readonly ComputeShader _shader;

        /// <summary>Compute shader kernel index for the zero-fill operation on buffer tails.</summary>
        private readonly int _zeroKernel;

        /// <summary>Whether this resizer has been disposed.</summary>
        private bool _disposed;

        /// <summary>Creates a GPU buffer resizer using the specified compute shader for copy/zero operations.</summary>
        public GpuBufferResizer(ComputeShader bufferCopyShader)
        {
            if (bufferCopyShader == null)
            {
                throw new ArgumentNullException(nameof(bufferCopyShader));
            }

            _shader = bufferCopyShader;
            _copyKernel = _shader.FindKernel("CSCopyBytes");
            _zeroKernel = _shader.FindKernel("CSZeroBytes");
        }

        /// <summary>
        ///     Immediately releases all queued buffers. Call from Dispose() of the owning
        ///     system to prevent leaks on shutdown.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = 0; i < _disposalQueue.Count; i++)
            {
                _disposalQueue[i].Buffer?.Dispose();
            }

            _disposalQueue.Clear();
        }

        /// <summary>
        ///     Allocates a new GPU buffer of <paramref name="newElementCount" /> elements,
        ///     copies the first <paramref name="usedElementCount" /> elements from
        ///     <paramref name="old" /> via a compute dispatch, zeroes the tail, then
        ///     retires <paramref name="old" /> for deferred disposal.
        ///     The <paramref name="target" /> flags must include
        ///     <see cref="GraphicsBuffer.Target.Raw" />.
        ///     Returns the new buffer, which is immediately usable.
        /// </summary>
        public GraphicsBuffer Resize(
            GraphicsBuffer old,
            int newElementCount,
            int usedElementCount,
            GraphicsBuffer.Target target,
            int elementStride)
        {
            GraphicsBuffer newBuffer = new(
                target,
                GraphicsBuffer.UsageFlags.None,
                newElementCount,
                elementStride);

            int usedBytes = usedElementCount * elementStride;
            int totalNewBytes = newElementCount * elementStride;
            int tailBytes = totalNewBytes - usedBytes;

            // Round up to 16-byte boundary for uint4 loads
            int copyBytes = AlignUp16(usedBytes);
            int zeroBytes = AlignUp16(tailBytes);

            if (copyBytes > 0 && old != null)
            {
                _shader.SetBuffer(_copyKernel, s_srcId, old);
                _shader.SetBuffer(_copyKernel, s_dstId, newBuffer);
                _shader.SetInt(s_byteCountId, copyBytes);

                int totalGroups = (copyBytes / 16 + 63) / 64;
                int groupsX = math.min(totalGroups, 65535);
                int groupsY = (totalGroups + 65534) / 65535;
                _shader.Dispatch(_copyKernel, groupsX, groupsY, 1);
            }

            if (zeroBytes > 0)
            {
                _shader.SetBuffer(_zeroKernel, s_dstId, newBuffer);
                _shader.SetInt(s_zeroStartId, usedBytes);
                _shader.SetInt(s_zeroByteCountId, zeroBytes);

                int totalGroups = (zeroBytes / 16 + 63) / 64;
                int groupsX = math.min(totalGroups, 65535);
                int groupsY = (totalGroups + 65534) / 65535;
                _shader.Dispatch(_zeroKernel, groupsX, groupsY, 1);
            }

            if (old != null)
            {
                _disposalQueue.Add(new DeferredDisposal
                {
                    Buffer = old, RetireFrame = Time.frameCount + RetireFrameDelay,
                });
            }

            return newBuffer;
        }

        /// <summary>
        ///     Drains the deferred disposal queue, releasing buffers whose retire frame
        ///     has arrived. Call once per frame at the start of GameLoop.Update.
        /// </summary>
        public void Tick()
        {
            int currentFrame = Time.frameCount;
            int writeIdx = 0;

            for (int i = 0; i < _disposalQueue.Count; i++)
            {
                DeferredDisposal entry = _disposalQueue[i];

                if (entry.RetireFrame <= currentFrame)
                {
                    entry.Buffer?.Dispose();
                }
                else
                {
                    _disposalQueue[writeIdx] = entry;
                    writeIdx++;
                }
            }

            int removeCount = _disposalQueue.Count - writeIdx;

            if (removeCount > 0)
            {
                _disposalQueue.RemoveRange(writeIdx, removeCount);
            }
        }

        /// <summary>Rounds a byte count up to the nearest 16-byte boundary for uint4 GPU loads.</summary>
        private static int AlignUp16(int value)
        {
            return value + 15 & ~15;
        }

        /// <summary>Tracks a retired GPU buffer and the frame count at which it can safely be disposed.</summary>
        private struct DeferredDisposal
        {
            /// <summary>The retired GPU buffer awaiting disposal.</summary>
            public GraphicsBuffer Buffer;

            /// <summary>The frame count after which the GPU is guaranteed to have finished reading.</summary>
            public int RetireFrame;
        }
    }
}
