using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class ChunkMeshStoreSubsystem : IGameSubsystem
    {
        private ChunkMeshStore _store;

        public string Name
        {
            get
            {
                return "ChunkMeshStore";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MaterialSubsystem),
            typeof(GpuBufferResizerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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
                UnityEngine.Debug.LogWarning(
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
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] HiZGenerate compute shader not found. " +
                    "Hi-Z occlusion culling will be disabled.");
            }

            _store = new ChunkMeshStore(
                materials.Opaque, materials.Cutout, materials.Translucent,
                cs.RenderDistance,
                cs.YLoadMin, cs.YLoadMax,
                cs.YUnloadMin, cs.YUnloadMax,
                cullShader, hiZShader, resizer,
                context.App.PipelineStats);

            // Set sea level for altitude-based tint adjustment in shader
            Shader.SetGlobalFloat("_SeaLevel", context.App.Settings.WorldGen.SeaLevel);

            context.Register(_store);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

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
