using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Ore;
using Lithforge.WorldGen.Pipeline;
using Lithforge.WorldGen.River;

using Unity.Collections;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the Burst generation pipeline with native biome and ore data
    ///     for terrain generation, caves, ores, and surface building.
    /// </summary>
    public sealed class WorldGenSubsystem : IGameSubsystem
    {
        /// <summary>Persistent NativeArray of baked biome data for Burst jobs.</summary>
        private NativeArray<NativeBiomeData> _nativeBiomeData;

        /// <summary>Persistent NativeArray of baked ore configurations for Burst jobs.</summary>
        private NativeArray<NativeOreConfig> _nativeOreConfigs;

        /// <summary>The owned generation pipeline.</summary>
        private GenerationPipeline _pipeline;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "WorldGen";
            }
        }

        /// <summary>Depends on chunk manager for chunk data storage.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Only created for sessions with local world generation (not pure clients).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld && config is not SessionConfig.Client;
        }

        /// <summary>Bakes biome and ore data to NativeArrays and creates the generation pipeline.</summary>
        public void Initialize(SessionContext context)
        {
            WorldGenSettings wg = context.App.Settings.WorldGen;

            NativeNoiseConfig terrainNoise = wg.TerrainNoise.ToNativeConfig();
            NativeNoiseConfig temperatureNoise = wg.TemperatureNoise.ToNativeConfig();
            NativeNoiseConfig humidityNoise = wg.HumidityNoise.ToNativeConfig();
            NativeNoiseConfig continentalnessNoise = wg.ContinentalnessNoise.ToNativeConfig();
            NativeNoiseConfig erosionNoise = wg.ErosionNoise.ToNativeConfig();
            NativeNoiseConfig caveNoise = wg.CaveNoise.ToNativeConfig();

            StateId stoneId = StateIdHelper.FindStateId(context.Content, "lithforge:stone");
            StateId airId = StateId.Air;
            StateId waterId = StateIdHelper.FindStateId(context.Content, "lithforge:water");

            // Build native biome data
            BiomeDefinition[] biomes = context.Content.BiomeDefinitions;
            _nativeBiomeData = new NativeArray<NativeBiomeData>(
                biomes.Length, Allocator.Persistent);

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeDefinition def = biomes[i];
                _nativeBiomeData[i] = new NativeBiomeData
                {
                    BiomeId = (byte)i,
                    TemperatureMin = def.TemperatureMin,
                    TemperatureMax = def.TemperatureMax,
                    TemperatureCenter = def.TemperatureCenter,
                    HumidityMin = def.HumidityMin,
                    HumidityMax = def.HumidityMax,
                    HumidityCenter = def.HumidityCenter,
                    TopBlock = StateIdHelper.FindStateIdForBlock(context.Content, def.TopBlock),
                    FillerBlock = StateIdHelper.FindStateIdForBlock(context.Content, def.FillerBlock),
                    StoneBlock = StateIdHelper.FindStateIdForBlock(context.Content, def.StoneBlock),
                    UnderwaterBlock = StateIdHelper.FindStateIdForBlock(context.Content, def.UnderwaterBlock),
                    FillerDepth = (byte)def.FillerDepth,
                    TreeDensity = def.TreeDensity,
                    TreeTemplateIndex = (byte)def.TreeType,
                    ContinentalnessCenter = def.ContinentalnessCenter,
                    ErosionCenter = def.ErosionCenter,
                    BaseHeight = def.BaseHeight,
                    HeightAmplitude = def.HeightAmplitude,
                    WaterColorPacked = PackColor(def.WaterColor),
                    WeightSharpness = def.WeightSharpness,
                    SurfaceFlags = BuildSurfaceFlags(def),
                };
            }

            // Verify BiomeData[i].BiomeId == i invariant
            for (int i = 0; i < biomes.Length; i++)
            {
                UnityEngine.Debug.Assert(
                    _nativeBiomeData[i].BiomeId == i,
                    $"[Lithforge] BiomeData invariant violated: BiomeData[{i}].BiomeId == {_nativeBiomeData[i].BiomeId}, expected {i}.");
            }

            // Build native ore configs
            OreDefinition[] ores = context.Content.OreDefinitions;
            _nativeOreConfigs = new NativeArray<NativeOreConfig>(
                ores.Length, Allocator.Persistent);

            for (int i = 0; i < ores.Length; i++)
            {
                OreDefinition def = ores[i];
                _nativeOreConfigs[i] = new NativeOreConfig
                {
                    OreStateId = StateIdHelper.FindStateIdForBlock(context.Content, def.OreBlock),
                    ReplaceStateId = StateIdHelper.FindStateIdForBlock(context.Content, def.ReplaceBlock),
                    MinY = def.MinY,
                    MaxY = def.MaxY,
                    VeinSize = def.VeinSize,
                    Frequency = def.Frequency,
                    OreType = (byte)(def.OreType == OreType.Scatter ? 0 : 1),
                };
            }

            StateId iceId = StateIdHelper.FindStateId(context.Content, "lithforge:ice");
            StateId gravelId = StateIdHelper.FindStateId(context.Content, "lithforge:gravel");
            StateId sandId = StateIdHelper.FindStateId(context.Content, "lithforge:sand");
            NativeRiverConfig riverConfig = wg.RiverNoise.ToNativeConfig();

            _pipeline = new GenerationPipeline(
                terrainNoise,
                temperatureNoise,
                humidityNoise,
                continentalnessNoise,
                erosionNoise,
                caveNoise,
                wg.CaveThreshold,
                wg.MinCarveY,
                wg.CaveSeedOffset1,
                wg.CaveSeedOffset2,
                wg.SeaLevelCarveBuffer,
                _nativeBiomeData,
                _nativeOreConfigs,
                context.Content.NativeStateRegistry.States,
                stoneId, airId, waterId,
                iceId, gravelId, sandId,
                wg.SeaLevel,
                riverConfig);

            context.Register(_pipeline);
            context.Register(new NativeBiomeDataHolder(_nativeBiomeData));
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes persistent NativeArrays of biome and ore data.</summary>
        public void Dispose()
        {
            if (_nativeBiomeData.IsCreated)
            {
                _nativeBiomeData.Dispose();
            }

            if (_nativeOreConfigs.IsCreated)
            {
                _nativeOreConfigs.Dispose();
            }
        }

        /// <summary>Builds the byte flag mask for biome surface properties (ocean, frozen, beach).</summary>
        private static byte BuildSurfaceFlags(BiomeDefinition def)
        {
            byte flags = 0;

            if (def.IsOcean)
            {
                flags |= NativeBiomeSurfaceFlags.IsOcean;
            }

            if (def.IsFrozen)
            {
                flags |= NativeBiomeSurfaceFlags.IsFrozen;
            }

            if (def.IsBeach)
            {
                flags |= NativeBiomeSurfaceFlags.IsBeach;
            }

            return flags;
        }

        /// <summary>Packs an RGBA color into a single uint32 for Burst-compatible storage.</summary>
        private static uint PackColor(Color c)
        {
            byte r = (byte)(Mathf.Clamp01(c.r) * 255f);
            byte g = (byte)(Mathf.Clamp01(c.g) * 255f);
            byte b = (byte)(Mathf.Clamp01(c.b) * 255f);
            byte a = (byte)(Mathf.Clamp01(c.a) * 255f);
            return (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | a;
        }
    }

    /// <summary>
    ///     Reference-type wrapper for <see cref="NativeArray{NativeBiomeData}"/>
    ///     so it can be registered in <see cref="SessionContext"/> (requires class constraint).
    /// </summary>
    public sealed class NativeBiomeDataHolder
    {
        /// <summary>Wraps the given NativeArray for session context registration.</summary>
        public NativeBiomeDataHolder(NativeArray<NativeBiomeData> data)
        {
            Data = data;
        }

        /// <summary>The wrapped NativeArray of biome data.</summary>
        public NativeArray<NativeBiomeData> Data { get; }
    }
}
