using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the GPU-driven chunk mesh store for indirect rendering.</summary>
    public sealed class ChunkMeshStoreSubsystem : IGameSubsystem
    {
        /// <summary>The owned chunk mesh store instance.</summary>
        private ChunkMeshStore _store;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ChunkMeshStore";
            }
        }

        /// <summary>Depends on materials and GPU buffer resizer for rendering setup.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MaterialSubsystem),
            typeof(GpuBufferResizerSubsystem),
        };

        /// <summary>Only created for sessions that render chunks.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the chunk mesh store with compute shaders, materials, and render distance.</summary>
        public void Initialize(SessionContext context)
        {
            VoxelMaterials materials = context.Get<VoxelMaterials>();
            GpuBufferResizer resizer = context.Get<GpuBufferResizer>();
            ChunkSettings cs = context.App.Settings.Chunk;

            ComputeShader cullShader = context.App.FrustumCullShader;

            if (cullShader == null)
            {
                cullShader = Resources.Load<ComputeShader>("FrustumCull");
            }

            if (cullShader == null)
            {
                context.App.Logger.LogWarning(
                    "[Lithforge] FrustumCull compute shader not found. " +
                    "GPU frustum culling will be disabled.");
            }

            ComputeShader hiZShader = context.App.HiZGenerateShader;

            if (hiZShader == null)
            {
                hiZShader = Resources.Load<ComputeShader>("HiZGenerate");
            }

            if (hiZShader == null)
            {
                context.App.Logger.LogWarning(
                    "[Lithforge] HiZGenerate compute shader not found. " +
                    "Hi-Z occlusion culling will be disabled.");
            }

            _store = new ChunkMeshStore(
                materials.Opaque, materials.Cutout, materials.Translucent,
                cs.RenderDistance,
                cs.YLoadMin, cs.YLoadMax,
                cs.YUnloadMin, cs.YUnloadMax,
                cullShader, hiZShader, resizer,
                context.App.PipelineStats,
                context.App.Logger);

            // Set sea level for altitude-based tint adjustment in shader
            Shader.SetGlobalFloat("_SeaLevel", context.App.Settings.WorldGen.SeaLevel);

            context.Register(_store);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the chunk mesh store and all GPU resources.</summary>
        public void Dispose()
        {
            if (_store != null)
            {
                _store.Dispose();
                _store = null;
            }
        }
    }
}
