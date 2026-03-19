using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     UI Toolkit element that paints a frame-time bar graph using Painter2D.
    ///     Replaces the Texture2D+SetPixels32 approach from the old DebugOverlayHUD.
    ///     Each sample is one pixel-wide bar, colored green/yellow/red by frame time.
    ///     Reference lines at 16.67ms (60fps) and 33.33ms (30fps).
    /// </summary>
    public sealed class FrameTimeGraphElement : VisualElement
    {
        /// <summary>Bar color for frames under 16.67ms (meeting 60 FPS target).</summary>
        private static readonly Color s_green = new(0f, 0.78f, 0f, 1f);

        /// <summary>Bar color for frames between 16.67ms and 33.33ms (30-60 FPS).</summary>
        private static readonly Color s_yellow = new(0.86f, 0.78f, 0f, 1f);

        /// <summary>Bar color for frames exceeding 33.33ms (below 30 FPS).</summary>
        private static readonly Color s_red = new(0.86f, 0.16f, 0.16f, 1f);

        /// <summary>Semi-transparent black background color for the graph area.</summary>
        private static readonly Color s_bgColor = new(0f, 0f, 0f, 0.63f);

        /// <summary>Reference line color for the 60 FPS (16.67ms) threshold.</summary>
        private static readonly Color s_line60 = new(0f, 0.7f, 0f, 0.35f);

        /// <summary>Reference line color for the 30 FPS (33.33ms) threshold.</summary>
        private static readonly Color s_line30 = new(0.86f, 0.16f, 0.16f, 0.35f);

        /// <summary>Number of valid entries in the history ring buffer.</summary>
        private int _filled;

        /// <summary>Current write position in the history ring buffer.</summary>
        private int _head;

        /// <summary>Frame time history ring buffer (shared reference from MetricsRegistry).</summary>
        private float[] _history;

        /// <summary>Registers the visual content generation callback for Painter2D rendering.</summary>
        public FrameTimeGraphElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>Updates the frame time data source for the next repaint cycle.</summary>
        public void SetData(float[] history, int head, int filled)
        {
            _history = history;
            _head = head;
            _filled = filled;
        }

        /// <summary>Paints the background, reference lines, and per-frame color-coded bars.</summary>
        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            Painter2D p = mgc.painter2D;
            float w = resolvedStyle.width;
            float h = resolvedStyle.height;

            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f || h < 1f)
            {
                return;
            }

            // Background
            p.fillColor = s_bgColor;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();

            if (_history == null || _filled < 2)
            {
                return;
            }

            float graphMaxMs = ComputeScale();

            // Reference lines
            DrawRefLine(p, w, h, 16.667f, graphMaxMs, s_line60);
            DrawRefLine(p, w, h, 33.333f, graphMaxMs, s_line30);

            // Draw bars — one pixel column per sample, newest on right
            int sampleCount = _filled < (int)w ? _filled : (int)w;

            for (int i = 0; i < sampleCount; i++)
            {
                int idx = (_head - sampleCount + i + _history.Length) % _history.Length;
                float ms = _history[idx];
                float barH = Mathf.Clamp(ms / graphMaxMs, 0f, 1f) * h;
                float x = w - sampleCount + i;

                Color barColor;

                if (ms < 16.667f)
                {
                    barColor = s_green;
                }
                else if (ms < 33.333f)
                {
                    barColor = s_yellow;
                }
                else
                {
                    barColor = s_red;
                }

                p.fillColor = barColor;
                p.BeginPath();
                p.MoveTo(new Vector2(x, h));
                p.LineTo(new Vector2(x + 1f, h));
                p.LineTo(new Vector2(x + 1f, h - barH));
                p.MoveTo(new Vector2(x, h - barH));
                p.ClosePath();
                p.Fill();
            }
        }

        /// <summary>Draws a horizontal reference line at the specified frame time threshold.</summary>
        private static void DrawRefLine(Painter2D p, float w, float h, float ms, float maxMs, Color color)
        {
            float y = h - ms / maxMs * h;

            if (y < 0f || y > h)
            {
                return;
            }

            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, y));
            p.LineTo(new Vector2(w, y));
            p.LineTo(new Vector2(w, y + 1f));
            p.LineTo(new Vector2(0f, y + 1f));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>Computes the Y-axis scale in milliseconds based on the peak frame time in history.</summary>
        private float ComputeScale()
        {
            float maxMs = 0f;

            for (int i = 0; i < _filled; i++)
            {
                float ms = _history[i];

                if (ms > maxMs)
                {
                    maxMs = ms;
                }
            }

            if (maxMs <= 20f)
            {
                return 20f;
            }

            if (maxMs <= 33.4f)
            {
                return 34f;
            }

            if (maxMs <= 50f)
            {
                return 50f;
            }

            if (maxMs <= 100f)
            {
                return 100f;
            }

            if (maxMs <= 200f)
            {
                return 200f;
            }

            return maxMs * 1.1f;
        }
    }
}
