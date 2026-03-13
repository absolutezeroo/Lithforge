using System.Collections.Generic;
using Lithforge.WorldGen.Climate;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Manages a global GPU texture storing biome climate parameters (temperature, humidity,
    /// biomeId) for shader-side colormap lookup. Uses toroidal addressing — the texture wraps
    /// and new data overwrites stale regions as the player moves.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class BiomeTintManager : System.IDisposable
    {
        private static readonly int _biomeParamMapId = Shader.PropertyToID("_BiomeParamMap");
        private static readonly int _biomeMapScaleId = Shader.PropertyToID("_BiomeMapScale");
        private static readonly int _grassColormapId = Shader.PropertyToID("_GrassColormap");
        private static readonly int _foliageColormapId = Shader.PropertyToID("_FoliageColormap");
        private static readonly int _waterColorLUTId = Shader.PropertyToID("_WaterColorLUT");

        private readonly int _mapSize;
        private readonly int _chunkSize;
        private readonly Texture2D _globalMap;
        private readonly Texture2D _staging;
        private readonly Texture2D _waterColorLUT;

        /// <summary>
        /// Tracks which chunk columns have been written to avoid redundant uploads.
        /// Key: (chunkX, chunkZ). Bounded by the number of simultaneously loaded chunk columns
        /// (approximately renderDistance^2); entries are removed on chunk unload.
        /// Owner: BiomeTintManager. Lifetime: application session.
        /// </summary>
        private readonly HashSet<int2> _writtenChunks = new HashSet<int2>();

        public BiomeTintManager(
            int mapSize,
            int chunkSize,
            Texture2D grassColormap,
            Texture2D foliageColormap,
            Color[] biomeWaterColors)
        {
            UnityEngine.Debug.Assert(mapSize % chunkSize == 0,
                $"BiomeTintManager: mapSize ({mapSize}) must be a multiple of chunkSize ({chunkSize}).");

            _mapSize = mapSize;
            _chunkSize = chunkSize;

            // RGBA32 = R8G8B8A8: R=temperature, G=humidity, B=biomeId, A=reserved
            _globalMap = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false, true);
            _globalMap.name = "GlobalBiomeParamMap";
            // Point filtering required: B channel stores discrete biomeId (integer 0-255).
            // Bilinear would interpolate biome IDs at chunk boundaries, corrupting water
            // color LUT lookups. Temperature/humidity in RG are smooth noise values that
            // don't need sub-texel interpolation.
            _globalMap.filterMode = FilterMode.Point;
            _globalMap.wrapMode = TextureWrapMode.Repeat;

            // Clear to default (temp=0.5, humidity=0.5, biomeId=0, reserved=255)
            NativeArray<byte> clearData = _globalMap.GetPixelData<byte>(0);

            for (int i = 0; i < clearData.Length; i += 4)
            {
                clearData[i] = 128;
                clearData[i + 1] = 128;
                clearData[i + 2] = 0;
                clearData[i + 3] = 255;
            }

            _globalMap.Apply(false, false);

            // Staging texture for chunk-sized uploads
            _staging = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false, true);
            _staging.name = "BiomeTintStaging";

            // Bind globally
            Shader.SetGlobalTexture(_biomeParamMapId, _globalMap);

            if (grassColormap != null)
            {
                Shader.SetGlobalTexture(_grassColormapId, grassColormap);
            }

            if (foliageColormap != null)
            {
                Shader.SetGlobalTexture(_foliageColormapId, foliageColormap);
            }

            // Set scale (xy=reserved, zw=1/mapSize for toroidal UV)
            float invMapSize = 1.0f / _mapSize;
            Shader.SetGlobalVector(_biomeMapScaleId, new Vector4(0, 0, invMapSize, invMapSize));

            // Build water color LUT (256x1, one pixel per biome)
            _waterColorLUT = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
            _waterColorLUT.name = "WaterColorLUT";
            _waterColorLUT.filterMode = FilterMode.Point;
            _waterColorLUT.wrapMode = TextureWrapMode.Clamp;

            Color32[] lutPixels = new Color32[256];

            for (int i = 0; i < 256; i++)
            {
                if (biomeWaterColors != null && i < biomeWaterColors.Length)
                {
                    Color c = biomeWaterColors[i];
                    lutPixels[i] = new Color32(
                        (byte)(c.r * 255f), (byte)(c.g * 255f),
                        (byte)(c.b * 255f), 255);
                }
                else
                {
                    // Default water blue
                    lutPixels[i] = new Color32(63, 118, 228, 255);
                }
            }

            _waterColorLUT.SetPixels32(lutPixels);
            _waterColorLUT.Apply(false, true);
            Shader.SetGlobalTexture(_waterColorLUTId, _waterColorLUT);
        }

        /// <summary>
        /// Writes a chunk's climate data (temperature, humidity, biomeId per column) into
        /// the global texture. Must be called on main thread (uses Graphics.CopyTexture).
        /// </summary>
        public void WriteChunkClimate(
            int3 chunkCoord,
            NativeArray<ClimateData> climateMap,
            NativeArray<byte> biomeMap)
        {
            int2 key = new int2(chunkCoord.x, chunkCoord.z);

            if (_writtenChunks.Contains(key))
            {
                return;
            }

            NativeArray<byte> stagingData = _staging.GetPixelData<byte>(0);

            for (int z = 0; z < _chunkSize; z++)
            {
                for (int x = 0; x < _chunkSize; x++)
                {
                    int colIdx = z * _chunkSize + x;
                    ClimateData climate = climateMap[colIdx];
                    byte biomeId = biomeMap[colIdx];
                    int pixelIdx = colIdx * 4;
                    stagingData[pixelIdx] = (byte)(Mathf.Clamp01(climate.Temperature) * 255f);
                    stagingData[pixelIdx + 1] = (byte)(Mathf.Clamp01(climate.Humidity) * 255f);
                    stagingData[pixelIdx + 2] = biomeId;
                    stagingData[pixelIdx + 3] = 255;
                }
            }

            _staging.Apply(false, false);

            // Compute destination position in global map (toroidal wrap)
            int worldBlockX = chunkCoord.x * _chunkSize;
            int worldBlockZ = chunkCoord.z * _chunkSize;
            int destX = Mod(worldBlockX, _mapSize);
            int destZ = Mod(worldBlockZ, _mapSize);

            Graphics.CopyTexture(
                _staging, 0, 0, 0, 0, _chunkSize, _chunkSize,
                _globalMap, 0, 0, destX, destZ);

            _writtenChunks.Add(key);
        }

        /// <summary>
        /// Called when a chunk column is unloaded. Allows re-writing if the player returns.
        /// </summary>
        public void OnChunkUnloaded(int3 chunkCoord)
        {
            _writtenChunks.Remove(new int2(chunkCoord.x, chunkCoord.z));
        }

        public void Dispose()
        {
            if (_globalMap != null)
            {
                Object.Destroy(_globalMap);
            }

            if (_staging != null)
            {
                Object.Destroy(_staging);
            }

            if (_waterColorLUT != null)
            {
                Object.Destroy(_waterColorLUT);
            }

            _writtenChunks.Clear();
        }

        private static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
