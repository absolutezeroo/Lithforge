using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates and manages the biome tint color manager for
    ///     applying per-biome grass, foliage, and water colors to chunk meshes.
    /// </summary>
    public sealed class BiomeTintSubsystem : IGameSubsystem
    {
        /// <summary>The owned biome tint manager instance.</summary>
        private BiomeTintManager _manager;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "BiomeTint";
            }
        }

        /// <summary>Depends on ChunkMeshStoreSubsystem for mesh store registration.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkMeshStoreSubsystem),
        };

        /// <summary>Only created for sessions that render chunks.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the BiomeTintManager from biome water colors and rendering settings.</summary>
        public void Initialize(SessionContext context)
        {
            RenderingSettings rs = context.App.Settings.Rendering;
            BiomeDefinition[] biomes = context.Content.BiomeDefinitions;
            Color[] biomeWaterColors = new Color[biomes.Length];

            for (int i = 0; i < biomes.Length; i++)
            {
                biomeWaterColors[i] = biomes[i].WaterColor;
            }

            _manager = new BiomeTintManager(
                rs.BiomeMapSize,
                ChunkConstants.Size,
                rs.GrassColormap,
                rs.FoliageColormap,
                biomeWaterColors);

            context.Register(_manager);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the biome tint manager and its GPU resources.</summary>
        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.Dispose();
                _manager = null;
            }
        }
    }
}
