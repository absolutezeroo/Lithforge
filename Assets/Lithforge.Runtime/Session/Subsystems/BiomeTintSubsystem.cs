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
    public sealed class BiomeTintSubsystem : IGameSubsystem
    {
        private BiomeTintManager _manager;

        public string Name
        {
            get
            {
                return "BiomeTint";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkMeshStoreSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

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
