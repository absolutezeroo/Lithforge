using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     UI Toolkit element that paints a chunk state minimap using Painter2D.
    ///     Each cell is a colored quad representing a chunk's state in the XZ plane
    ///     at the camera's Y level. Camera chunk is marked white.
    /// </summary>
    public sealed class MinimapElement : VisualElement
    {
        /// <summary>Color palette indexed by ChunkState ordinal for minimap cell rendering.</summary>
        private static readonly Color[] s_stateColors =
        {
            new(0.08f, 0.08f, 0.08f, 1f), // Unloaded
            new(0.31f, 0.31f, 0.31f, 1f), // Loading
            new(0.78f, 0.47f, 0f, 1f),    // Generating
            new(0.71f, 0.39f, 0f, 1f),    // Decorating
            new(0.71f, 0f, 0.71f, 1f),    // RelightPending
            new(0f, 0.47f, 0.78f, 1f),    // Generated
            new(0.78f, 0.78f, 0f, 1f),    // Meshing
            new(0f, 0.71f, 0f, 1f),       // Ready
        };
        /// <summary>Chunk manager for querying chunk state at each grid position.</summary>
        private ChunkManager _chunkManager;

        /// <summary>Main camera for determining the center chunk of the minimap view.</summary>
        private Camera _mainCamera;

        /// <summary>Registers the visual content generation callback for Painter2D rendering.</summary>
        public MinimapElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>Sets the chunk manager and camera references needed for rendering.</summary>
        public void Initialize(ChunkManager chunkManager, Camera mainCamera)
        {
            _chunkManager = chunkManager;
            _mainCamera = mainCamera;
        }

        /// <summary>Paints the minimap grid with color-coded chunk states and a white camera marker.</summary>
        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_chunkManager == null || _mainCamera == null)
            {
                return;
            }

            Painter2D p = mgc.painter2D;
            float w = resolvedStyle.width;
            float h = resolvedStyle.height;

            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f || h < 1f)
            {
                return;
            }

            // Background
            p.fillColor = new Color(0f, 0f, 0f, 0.65f);
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();

            int rd = _chunkManager.RenderDistance;
            int gridSize = rd * 2 + 1;
            float cellW = w / gridSize;
            float cellH = h / gridSize;

            Vector3 camPos = _mainCamera.transform.position;
            int camChunkX = Mathf.FloorToInt(camPos.x / ChunkConstants.Size);
            int camChunkY = Mathf.FloorToInt(camPos.y / ChunkConstants.Size);
            int camChunkZ = Mathf.FloorToInt(camPos.z / ChunkConstants.Size);

            for (int dx = -rd; dx <= rd; dx++)
            {
                for (int dz = -rd; dz <= rd; dz++)
                {
                    int3 coord = new(camChunkX + dx, camChunkY, camChunkZ + dz);
                    ManagedChunk chunk = _chunkManager.GetChunk(coord);

                    Color color;

                    if (chunk == null)
                    {
                        color = s_stateColors[0];
                    }
                    else
                    {
                        int stateIdx = (int)chunk.State;

                        if (stateIdx >= 0 && stateIdx < s_stateColors.Length)
                        {
                            color = s_stateColors[stateIdx];
                        }
                        else
                        {
                            color = s_stateColors[0];
                        }

                        if (chunk.NeedsRemesh)
                        {
                            color.r = Mathf.Min(1f, color.r + 0.4f);
                            color.g *= 0.5f;
                            color.b *= 0.5f;
                        }
                    }

                    float x = (dx + rd) * cellW;
                    float y = (dz + rd) * cellH;

                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, y));
                    p.LineTo(new Vector2(x + cellW, y));
                    p.LineTo(new Vector2(x + cellW, y + cellH));
                    p.LineTo(new Vector2(x, y + cellH));
                    p.ClosePath();
                    p.Fill();
                }
            }

            // Camera center marker (white)
            float cx = rd * cellW;
            float cy = rd * cellH;
            p.fillColor = Color.white;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy));
            p.LineTo(new Vector2(cx + cellW, cy));
            p.LineTo(new Vector2(cx + cellW, cy + cellH));
            p.LineTo(new Vector2(cx, cy + cellH));
            p.ClosePath();
            p.Fill();
        }
    }
}
