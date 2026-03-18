using System;
using System.Collections.Generic;

using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class GpuBufferResizerSubsystem : IGameSubsystem
    {
        private GpuBufferResizer _resizer;

        public string Name
        {
            get
            {
                return "GpuBufferResizer";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

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
