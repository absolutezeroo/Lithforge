using System;
using System.Collections.Generic;

using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the GPU buffer resizer for dynamic buffer capacity growth.</summary>
    public sealed class GpuBufferResizerSubsystem : IGameSubsystem
    {
        /// <summary>The owned GPU buffer resizer instance.</summary>
        private GpuBufferResizer _resizer;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "GpuBufferResizer";
            }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Loads the BufferCopy compute shader and creates the resizer.</summary>
        public void Initialize(SessionContext context)
        {
            ComputeShader copyShader = context.App.BufferCopyShader;

            if (copyShader == null)
            {
                copyShader = Resources.Load<ComputeShader>("BufferCopy");
            }

            if (copyShader == null)
            {
                UnityEngine.Debug.LogError(
                    "[Lithforge] BufferCopy compute shader not found. " +
                    "Assign it in the Inspector or place it in a Resources folder.");
            }

            _resizer = new GpuBufferResizer(copyShader);
            context.Register(_resizer);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the GPU buffer resizer.</summary>
        public void Dispose()
        {
            if (_resizer != null)
            {
                _resizer.Dispose();
                _resizer = null;
            }
        }
    }
}
