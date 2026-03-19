using Lithforge.Voxel.Chunk;

using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Renders chunk border wireframes via GL.Lines in world space.
    ///     Activated by F3+G sub-toggle in F3DebugOverlay.
    ///     Draws 12 edges per chunk box within a configurable radius around the camera.
    /// </summary>
    public sealed class ChunkBorderRenderer : MonoBehaviour
    {
        /// <summary>Wireframe color for standard chunk boundaries (yellow, semi-transparent).</summary>
        private static readonly Color s_chunkColor = new(1f, 1f, 0f, 0.4f);

        /// <summary>Wireframe color for region boundaries at 32-chunk intervals (cyan, semi-transparent).</summary>
        private static readonly Color s_regionColor = new(0f, 1f, 1f, 0.6f);

        /// <summary>Radius in chunks around the camera within which borders are drawn.</summary>
        private int _drawRadius = 3;

        /// <summary>Unlit line material created from Hidden/Internal-Colored shader with alpha blending.</summary>
        private Material _lineMaterial;

        /// <summary>Main camera reference for determining the camera chunk position.</summary>
        private Camera _mainCamera;

        /// <summary>Metrics registry for reading the current camera chunk coordinates.</summary>
        private MetricsRegistry _metrics;

        /// <summary>Whether chunk border wireframes are currently being rendered.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Destroys the dynamically created line material to avoid shader leaks.</summary>
        private void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                DestroyImmediate(_lineMaterial);
            }
        }

        /// <summary>Draws GL.Lines wireframe boxes for each chunk within the draw radius.</summary>
        private void OnRenderObject()
        {
            if (!IsVisible || _metrics == null || _mainCamera == null)
            {
                return;
            }

            MetricSnapshot snap = _metrics.CurrentSnapshot;
            int size = ChunkConstants.Size;
            int camChunkX = snap.ChunkX;
            int camChunkY = snap.ChunkY;
            int camChunkZ = snap.ChunkZ;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            for (int dx = -_drawRadius; dx <= _drawRadius; dx++)
            {
                for (int dy = -_drawRadius; dy <= _drawRadius; dy++)
                {
                    for (int dz = -_drawRadius; dz <= _drawRadius; dz++)
                    {
                        int wx = camChunkX + dx;
                        int wy = camChunkY + dy;
                        int wz = camChunkZ + dz;

                        float bx = wx * size;
                        float by = wy * size;
                        float bz = wz * size;

                        // Use region color for chunks on 32-chunk boundaries (region edges)
                        bool isRegionEdge = wx % 32 == 0 || wz % 32 == 0;
                        Color color = isRegionEdge ? s_regionColor : s_chunkColor;
                        GL.Color(color);

                        DrawWireBox(bx, by, bz, bx + size, by + size, bz + size);
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>Initializes the renderer with metrics, camera, and draw radius, creating the line material.</summary>
        public void Initialize(MetricsRegistry metrics, Camera mainCamera, int drawRadius)
        {
            _metrics = metrics;
            _mainCamera = mainCamera;
            _drawRadius = drawRadius;

            Shader lineShader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(lineShader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        /// <summary>Sets the visibility of the chunk border wireframes.</summary>
        public void SetVisible(bool visible)
        {
            IsVisible = visible;
        }

        /// <summary>Emits GL.Vertex3 calls for 12 wireframe edges of an axis-aligned box.</summary>
        private static void DrawWireBox(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            // Bottom face (4 edges)
            GL.Vertex3(x0, y0, z0);
            GL.Vertex3(x1, y0, z0);
            GL.Vertex3(x1, y0, z0);
            GL.Vertex3(x1, y0, z1);
            GL.Vertex3(x1, y0, z1);
            GL.Vertex3(x0, y0, z1);
            GL.Vertex3(x0, y0, z1);
            GL.Vertex3(x0, y0, z0);

            // Top face (4 edges)
            GL.Vertex3(x0, y1, z0);
            GL.Vertex3(x1, y1, z0);
            GL.Vertex3(x1, y1, z0);
            GL.Vertex3(x1, y1, z1);
            GL.Vertex3(x1, y1, z1);
            GL.Vertex3(x0, y1, z1);
            GL.Vertex3(x0, y1, z1);
            GL.Vertex3(x0, y1, z0);

            // Vertical edges (4 edges)
            GL.Vertex3(x0, y0, z0);
            GL.Vertex3(x0, y1, z0);
            GL.Vertex3(x1, y0, z0);
            GL.Vertex3(x1, y1, z0);
            GL.Vertex3(x1, y0, z1);
            GL.Vertex3(x1, y1, z1);
            GL.Vertex3(x0, y0, z1);
            GL.Vertex3(x0, y1, z1);
        }
    }
}
