using Lithforge.Runtime.Content.Settings;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Renders a wireframe cube highlight around the targeted block.
    /// Uses a LineRenderer to draw the 12 edges of a cube.
    /// </summary>
    public sealed class BlockHighlight : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private bool _visible;
        private float _expand = 0.005f;

        // A cube has 12 edges. We draw them as a continuous line strip
        // visiting all edges by revisiting some vertices.
        // Path: 0->1->2->3->0->4->5->1->5->6->2->6->7->3->7->4
        private static readonly int[] _lineIndices = new int[]
        {
            0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4
        };

        private void Awake()
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = false;
            _lineRenderer.positionCount = _lineIndices.Length;
            _lineRenderer.numCapVertices = 0;
            _lineRenderer.numCornerVertices = 0;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.enabled = false;
            _visible = false;
        }

        public void Initialize(RenderingSettings settings)
        {
            _lineRenderer.startWidth = settings.BlockHighlightLineWidth;
            _lineRenderer.endWidth = settings.BlockHighlightLineWidth;
            _lineRenderer.startColor = settings.BlockHighlightColor;
            _lineRenderer.endColor = settings.BlockHighlightColor;
            _expand = settings.BlockHighlightExpand;
        }

        /// <summary>
        /// Updates the highlight to show around the given block coordinate.
        /// </summary>
        public void SetTarget(int3 blockCoord)
        {
            if (!_visible)
            {
                _lineRenderer.enabled = true;
                _visible = true;
            }

            // Slight expansion to avoid z-fighting with block faces
            float3 min = new float3(blockCoord) - new float3(_expand);
            float3 max = new float3(blockCoord) + new float3(1f + _expand);

            // 8 corners of the cube
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, min.y, max.z);
            corners[3] = new Vector3(min.x, min.y, max.z);
            corners[4] = new Vector3(min.x, max.y, min.z);
            corners[5] = new Vector3(max.x, max.y, min.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);

            for (int i = 0; i < _lineIndices.Length; i++)
            {
                _lineRenderer.SetPosition(i, corners[_lineIndices[i]]);
            }
        }

        /// <summary>
        /// Hides the block highlight.
        /// </summary>
        public void Hide()
        {
            if (_visible)
            {
                _lineRenderer.enabled = false;
                _visible = false;
            }
        }
    }
}
