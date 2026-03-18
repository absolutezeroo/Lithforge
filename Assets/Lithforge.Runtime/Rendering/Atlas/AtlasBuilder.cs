using System.Collections.Generic;
using Lithforge.Core.Logging;
using Lithforge.Runtime.Content.Models;
using Lithforge.Voxel.Block;
using UnityEngine;

namespace Lithforge.Runtime.Rendering.Atlas
{
    /// <summary>
    /// Builds a Texture2DArray from direct Texture2D references.
    /// Discovers all unique textures referenced by resolved block states,
    /// reads their pixels, and assigns sequential array indices.
    /// Index 0 is always the missing texture (magenta checkerboard).
    /// </summary>
    public sealed class AtlasBuilder
    {
        private readonly int _tileSize;
        private readonly Core.Logging.ILogger _logger;

        public AtlasBuilder(Core.Logging.ILogger logger, int tileSize = 16)
        {
            _logger = logger;
            _tileSize = tileSize;
        }

        /// <summary>
        /// Builds the texture atlas from resolved face textures.
        /// </summary>
        public AtlasResult Build(Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces)
        {
            // Collect all unique Texture2D refs (null = missing)
            HashSet<Texture2D> uniqueTextures = new();

            foreach (KeyValuePair<StateId, ResolvedFaceTextures2D> kvp in resolvedFaces)
            {
                ResolvedFaceTextures2D faces = kvp.Value;

                // Base textures
                if (faces.North != null) { uniqueTextures.Add(faces.North); }
                if (faces.South != null) { uniqueTextures.Add(faces.South); }
                if (faces.East != null) { uniqueTextures.Add(faces.East); }
                if (faces.West != null) { uniqueTextures.Add(faces.West); }
                if (faces.Up != null) { uniqueTextures.Add(faces.Up); }
                if (faces.Down != null) { uniqueTextures.Add(faces.Down); }

                // Overlay textures
                if (faces.OverlayNorth != null) { uniqueTextures.Add(faces.OverlayNorth); }
                if (faces.OverlaySouth != null) { uniqueTextures.Add(faces.OverlaySouth); }
                if (faces.OverlayEast != null) { uniqueTextures.Add(faces.OverlayEast); }
                if (faces.OverlayWest != null) { uniqueTextures.Add(faces.OverlayWest); }
                if (faces.OverlayUp != null) { uniqueTextures.Add(faces.OverlayUp); }
                if (faces.OverlayDown != null) { uniqueTextures.Add(faces.OverlayDown); }
            }

            // Build index mapping: 0 = missing, then each unique texture
            Dictionary<Texture2D, int> indexByTexture = new();
            List<Texture2D> orderedTextures = new();

            // Reserve index 0 for missing texture (null key not stored — handled by lookup miss)
            orderedTextures.Add(null);

            foreach (Texture2D tex in uniqueTextures)
            {
                int nextIndex = orderedTextures.Count;
                indexByTexture[tex] = nextIndex;
                orderedTextures.Add(tex);
            }

            int sliceCount = orderedTextures.Count;

            _logger.LogInfo($"Atlas: {sliceCount} texture slices ({sliceCount - 1} unique + 1 missing).");

            // Create Texture2DArray
            Texture2DArray textureArray = new(
                _tileSize, _tileSize, sliceCount,
                TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

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

            // Copy each texture into its slice via RenderTexture blit.
            // This avoids requiring isReadable on source textures.
            for (int i = 1; i < orderedTextures.Count; i++)
            {
                Texture2D sourceTex = orderedTextures[i];

                if (sourceTex == null)
                {
                    textureArray.SetPixelData(magentaPixels, 0, i);
                    continue;
                }

                if (sourceTex.width != _tileSize || sourceTex.height != _tileSize)
                {
                    _logger.LogWarning(
                        $"Texture '{sourceTex.name}' is {sourceTex.width}x{sourceTex.height}, expected {_tileSize}x{_tileSize}.");
                }

                // Blit through RenderTexture so we can read any texture,
                // even compressed or non-readable ones.
                Texture2D readable = new(_tileSize, _tileSize, TextureFormat.RGBA32, false);
                RenderTexture rt = RenderTexture.GetTemporary(_tileSize, _tileSize, 0, RenderTextureFormat.ARGB32);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(sourceTex, rt);
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, _tileSize, _tileSize), 0, 0);
                readable.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color32[] pixels = readable.GetPixels32();
                textureArray.SetPixelData(pixels, 0, i);
                Object.DestroyImmediate(readable);
            }

            textureArray.Apply(false, true);

            return new AtlasResult(textureArray, indexByTexture, 0);
        }
    }
}
