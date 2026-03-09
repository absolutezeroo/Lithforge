using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;
using UnityEngine;

namespace Lithforge.Runtime.Rendering.Atlas
{
    /// <summary>
    /// Builds a Texture2DArray from content PNG files.
    /// Discovers all unique textures referenced by resolved block states,
    /// loads them from StreamingAssets, and assigns sequential array indices.
    /// Index 0 is always the missing texture (magenta).
    /// </summary>
    public sealed class AtlasBuilder
    {
        private const int _tileSize = 16;
        private readonly Core.Logging.ILogger _logger;

        public AtlasBuilder(Core.Logging.ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Builds the texture atlas from resolved face textures.
        /// </summary>
        public AtlasResult Build(
            Dictionary<StateId, ResolvedFaceTextures> resolvedFaces,
            string contentRoot)
        {
            // Collect all unique texture ResourceIds
            HashSet<ResourceId> uniqueTextures = new HashSet<ResourceId>();

            foreach (KeyValuePair<StateId, ResolvedFaceTextures> kvp in resolvedFaces)
            {
                ResolvedFaceTextures faces = kvp.Value;
                uniqueTextures.Add(faces.North);
                uniqueTextures.Add(faces.South);
                uniqueTextures.Add(faces.East);
                uniqueTextures.Add(faces.West);
                uniqueTextures.Add(faces.Up);
                uniqueTextures.Add(faces.Down);
            }

            // Build index mapping: 0 = missing, then each unique texture
            Dictionary<ResourceId, int> indexByTexture = new Dictionary<ResourceId, int>();
            List<ResourceId> orderedTextures = new List<ResourceId>();

            // Reserve index 0 for missing texture
            ResourceId missingId = new ResourceId("lithforge", "block/missing");
            indexByTexture[missingId] = 0;
            orderedTextures.Add(missingId);

            foreach (ResourceId texId in uniqueTextures)
            {
                if (!indexByTexture.ContainsKey(texId))
                {
                    int nextIndex = orderedTextures.Count;
                    indexByTexture[texId] = nextIndex;
                    orderedTextures.Add(texId);
                }
            }

            int sliceCount = orderedTextures.Count;
            _logger.LogInfo($"Atlas: {sliceCount} texture slices ({sliceCount - 1} unique + 1 missing).");

            // Create Texture2DArray
            Texture2DArray textureArray = new Texture2DArray(
                _tileSize, _tileSize, sliceCount,
                TextureFormat.RGBA32, false, false);
            textureArray.filterMode = FilterMode.Point;
            textureArray.wrapMode = TextureWrapMode.Repeat;

            // Slice 0: magenta missing texture
            Color32[] magentaPixels = new Color32[_tileSize * _tileSize];

            for (int p = 0; p < magentaPixels.Length; p++)
            {
                // Checkerboard magenta/black for visibility
                int px = p % _tileSize;
                int py = p / _tileSize;
                bool checker = ((px / 4) + (py / 4)) % 2 == 0;
                magentaPixels[p] = checker
                    ? new Color32(255, 0, 255, 255)
                    : new Color32(0, 0, 0, 255);
            }

            textureArray.SetPixelData(magentaPixels, 0, 0);

            // Load each texture
            for (int i = 1; i < orderedTextures.Count; i++)
            {
                ResourceId texId = orderedTextures[i];
                string texturePath = Path.Combine(
                    contentRoot, "assets",
                    texId.Namespace,
                    "textures",
                    texId.Name + ".png");

                if (File.Exists(texturePath))
                {
                    byte[] pngBytes = File.ReadAllBytes(texturePath);
                    Texture2D tempTex = new Texture2D(_tileSize, _tileSize, TextureFormat.RGBA32, false);
                    tempTex.LoadImage(pngBytes);

                    // Resize if needed
                    if (tempTex.width != _tileSize || tempTex.height != _tileSize)
                    {
                        _logger.LogWarning(
                            $"Texture '{texId}' is {tempTex.width}x{tempTex.height}, expected {_tileSize}x{_tileSize}. Using as-is.");
                    }

                    textureArray.SetPixelData(tempTex.GetRawTextureData<byte>(), 0, i);
                    Object.DestroyImmediate(tempTex);
                }
                else
                {
                    _logger.LogWarning($"Texture file not found: {texturePath}. Using missing texture.");
                    textureArray.SetPixelData(magentaPixels, 0, i);
                }
            }

            textureArray.Apply(false, true);

            return new AtlasResult(textureArray, indexByTexture, 0);
        }
    }
}
