using System.Collections.Generic;
using Lithforge.WorldGen.Climate;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Manages a global GPU texture storing biome climate parameters (temperature, humidity)
    /// for shader-side colormap lookup. Uses toroidal addressing — the texture wraps and
    /// new data overwrites stale regions as the player moves.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class BiomeTintManager : System.IDisposable
    {
        private static readonly int _biomeParamMapId = Shader.PropertyToID("_BiomeParamMap");
        private static readonly int _biomeMapTransformId = Shader.PropertyToID("_BiomeMapTransform");
        private static readonly int _grassColormapId = Shader.PropertyToID("_GrassColormap");
        private static readonly int _foliageColormapId = Shader.PropertyToID("_FoliageColormap");

        private readonly int _mapSize;
        private readonly int _chunkSize;
        private readonly Texture2D _globalMap;
        private readonly Texture2D _staging;

        /// <summary>
        /// Tracks which chunk columns have been written to avoid redundant uploads.
        /// Key: (chunkX, chunkZ).
        /// Owner: BiomeTintManager. Lifetime: application session.
        /// </summary>
        private readonly HashSet<int2> _writtenChunks = new HashSet<int2>();

        public BiomeTintManager(
            int mapSize,
            int chunkSize,
            Texture2D grassColormap,
            Texture2D foliageColormap)
        {
            _mapSize = mapSize;
            _chunkSize = chunkSize;

            // RG16 = R8G8, two 8-bit channels, 16 bits per texel
            _globalMap = new Texture2D(mapSize, mapSize, TextureFormat.RG16, false, true);
            _globalMap.name = "GlobalBiomeParamMap";
            _globalMap.filterMode = FilterMode.Bilinear;
            _globalMap.wrapMode = TextureWrapMode.Repeat;

            // Clear to default (temp=0.5, humidity=0.5)
            NativeArray<byte> clearData = _globalMap.GetPixelData<byte>(0);

            for (int i = 0; i < clearData.Length; i += 2)
            {
                clearData[i] = 128;
                clearData[i + 1] = 128;
            }

            _globalMap.Apply(false, false);

            // Staging texture for chunk-sized uploads
            _staging = new Texture2D(chunkSize, chunkSize, TextureFormat.RG16, false, true);
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

            // Set initial transform
            float invMapSize = 1.0f / _mapSize;
            Shader.SetGlobalVector(_biomeMapTransformId, new Vector4(0, 0, invMapSize, invMapSize));
        }

        /// <summary>
        /// Writes a chunk's climate data (temperature + humidity per column) into the global texture.
        /// Must be called on main thread (uses Graphics.CopyTexture).
        /// </summary>
        public void WriteChunkClimate(int3 chunkCoord, NativeArray<ClimateData> climateMap)
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
                    int pixelIdx = (z * _chunkSize + x) * 2;
                    stagingData[pixelIdx] = (byte)(Mathf.Clamp01(climate.Temperature) * 255f);
                    stagingData[pixelIdx + 1] = (byte)(Mathf.Clamp01(climate.Humidity) * 255f);
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

            _writtenChunks.Clear();
        }

        private static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
