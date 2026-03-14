using System.Text;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Debug
{
    public sealed class DebugOverlayHUD : MonoBehaviour
    {
        private GameLoop _gameLoop;
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private PlayerController _playerController;
        private ChunkPool _chunkPool;

        private Camera _mainCamera;
        private float _fps;
        private float _fpsTimer;
        private int _frameCount;
        private GUIStyle _style;
        private GUIStyle _smallStyle;
        private GUIStyle _monoStyle;
        private Texture2D _bgTexture;
        private Texture2D _barTexture;
        private bool _visible = true;
        private bool _benchmarkMode;

        // Settings
        private float _fpsSampleInterval = 0.5f;
        private float _overlayBackgroundAlpha = 0.6f;
        private int _overlayMinFontSize = 18;
        private int _overlayScreenDivisor = 50;
        private int _overlayPanelWidth = 420;
        private int _overlayPadding = 8;
        private int _overlayLineSpacing = 6;

        // Frame time graph
        private const int GraphSamples = 300;
        private const int GraphWidth = 302;
        private const int GraphHeight = 120;
        private readonly float[] _frameTimes = new float[GraphSamples];
        private int _frameTimeHead;
        private int _frameTimeFilled;
        private Texture2D _graphTexture;
        private readonly Color32[] _graphPixels = new Color32[GraphWidth * GraphHeight];

        private static readonly Color32 ColorGreen = new Color32(0, 200, 0, 255);
        private static readonly Color32 ColorYellow = new Color32(220, 200, 0, 255);
        private static readonly Color32 ColorRed = new Color32(220, 40, 40, 255);
        private static readonly Color32 ColorBg = new Color32(0, 0, 0, 160);
        private static readonly Color32 ColorLine60 = new Color32(0, 180, 0, 80);
        private static readonly Color32 ColorLine30 = new Color32(220, 40, 40, 80);

        // Chunk minimap
        private Texture2D _minimapTexture;
        private Color32[] _minimapPixels;
        private int _minimapSize;
        private int _minimapPixelScale;

        private static readonly Color32[] _stateColors = new Color32[]
        {
            new Color32(20, 20, 20, 255),       // Unloaded
            new Color32(80, 80, 80, 255),        // Loading
            new Color32(200, 120, 0, 255),       // Generating
            new Color32(180, 100, 0, 255),       // Decorating
            new Color32(180, 0, 180, 255),       // RelightPending
            new Color32(0, 120, 200, 255),       // Generated
            new Color32(200, 200, 0, 255),       // Meshing
            new Color32(0, 180, 0, 255),         // Ready
        };

        private static readonly string[] _stateNames = new string[]
        {
            "Empty", "Load", "Gen", "Deco", "Relit", "Done", "Mesh", "Ready",
        };

        // Pre-allocated StringBuilders for zero-alloc text building
        private readonly StringBuilder _timingBuilder = new StringBuilder(1024);
        private readonly StringBuilder _statsBuilder = new StringBuilder(1024);
        private readonly StringBuilder _leftBuilder = new StringBuilder(256);

        // Bar rendering colors
        private static readonly Color _barGreen = new Color(0f, 0.78f, 0f, 1f);
        private static readonly Color _barYellow = new Color(0.86f, 0.78f, 0f, 1f);
        private static readonly Color _barRed = new Color(0.86f, 0.16f, 0.16f, 1f);

        // Section display order (excludes UpdateTotal and Frame which are shown separately)
        private static readonly int[] _displaySections = new int[]
        {
            FrameProfiler.PollGen,
            FrameProfiler.PollMesh,
            FrameProfiler.PollLOD,
            FrameProfiler.LoadQueue,
            FrameProfiler.Unload,
            FrameProfiler.SchedGen,
            FrameProfiler.CrossLight,
            FrameProfiler.Relight,
            FrameProfiler.LODLevels,
            FrameProfiler.SchedMesh,
            FrameProfiler.SchedLOD,
            FrameProfiler.Render,
        };

        // Padded section names for alignment (12 chars each)
        private static readonly string[] _paddedNames = new string[]
        {
            "PollGen:    ",
            "PollMesh:   ",
            "PollLOD:    ",
            "LoadQueue:  ",
            "Unload:     ",
            "SchedGen:   ",
            "CrossLight: ",
            "Relight:    ",
            "LODLevels:  ",
            "SchedMesh:  ",
            "SchedLOD:   ",
            "Render:     ",
        };

        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        public void Initialize(
            GameLoop gameLoop,
            ChunkManager chunkManager,
            DebugSettings settings,
            ChunkMeshStore chunkMeshStore = null,
            PlayerController playerController = null,
            ChunkPool chunkPool = null)
        {
            _gameLoop = gameLoop;
            _chunkManager = chunkManager;
            _fpsSampleInterval = settings.FpsSampleInterval;
            _overlayBackgroundAlpha = settings.OverlayBackgroundAlpha;
            _overlayMinFontSize = settings.OverlayMinFontSize;
            _overlayScreenDivisor = settings.OverlayScreenDivisor;
            _overlayPanelWidth = settings.OverlayPanelWidth;
            _overlayPadding = settings.OverlayPadding;
            _overlayLineSpacing = settings.OverlayLineSpacing;
            _chunkMeshStore = chunkMeshStore;
            _playerController = playerController;
            _chunkPool = chunkPool;
            _visible = settings.ShowDebugOverlay;
            _mainCamera = Camera.main;

            _graphTexture = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false);
            _graphTexture.filterMode = FilterMode.Point;
            _graphTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float frameMs = dt * 1000f;

            _frameCount++;
            _fpsTimer += dt;

            float sampleInterval = frameMs > 33f ? 0.15f : _fpsSampleInterval;

            if (_fpsTimer >= sampleInterval)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }

            _frameTimes[_frameTimeHead] = frameMs;
            _frameTimeHead = (_frameTimeHead + 1) % GraphSamples;

            if (_frameTimeFilled < GraphSamples)
            {
                _frameTimeFilled++;
            }

            // Toggle benchmark mode with F4
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
            {
                _benchmarkMode = !_benchmarkMode;

                if (_benchmarkMode)
                {
                    FrameProfiler.Enabled = true;
                    PipelineStats.Enabled = true;
                }
            }

            // Update chunk histogram and minimap when benchmark mode is active
            if (_benchmarkMode && PipelineStats.Enabled && _chunkManager != null && _chunkPool != null)
            {
                PipelineStats.UpdateChunkHistogram(_chunkManager, _chunkPool);

                // Update free list size from opaque buffer
                if (_chunkMeshStore != null)
                {
                    PipelineStats.FreeListSize = _chunkMeshStore.OpaqueBuffer.FreeRegionCount;
                }

                EnsureMinimapTexture();
                UpdateMinimap();
            }
        }

        private void EnsureStyle()
        {
            if (_style != null)
            {
                return;
            }

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, _overlayBackgroundAlpha));
            _bgTexture.Apply();

            _barTexture = new Texture2D(1, 1);
            _barTexture.SetPixel(0, 0, Color.white);
            _barTexture.Apply();

            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = Mathf.Max(_overlayMinFontSize, Screen.height / _overlayScreenDivisor);
            _style.normal.textColor = Color.white;
            _style.fontStyle = FontStyle.Bold;

            _smallStyle = new GUIStyle(_style);
            _smallStyle.fontSize = Mathf.Max(12, _style.fontSize - 4);

            _monoStyle = new GUIStyle(_smallStyle);
            _monoStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", _smallStyle.fontSize);

            if (_monoStyle.font == null)
            {
                _monoStyle.font = Font.CreateDynamicFontFromOSFont("Courier New", _smallStyle.fontSize);
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureStyle();

            DrawLeftPanel();

            if (_benchmarkMode)
            {
                DrawBenchmarkPanel();
            }

            if (_frameTimeFilled >= 2 && _graphTexture != null)
            {
                DrawFrameTimeGraph();
            }

            if (_benchmarkMode && _minimapTexture != null)
            {
                DrawMinimap();
            }
        }

        private void DrawLeftPanel()
        {
            int lineHeight = _style.fontSize + _overlayLineSpacing;
            int padding = _overlayPadding;
            int y = padding;
            int x = padding;
            int lineCount = 1; // FPS is always rendered
            int labelWidth = _overlayPanelWidth - padding * 2;

            if (_mainCamera != null) { lineCount++; }
            if (_chunkManager != null) { lineCount++; }
            if (_chunkMeshStore != null) { lineCount += 2; } // renderers + culling status
            if (_gameLoop != null) { lineCount += 2; }
            if (_playerController != null && _playerController.IsFlying) { lineCount++; }

            // Background panel
            int panelHeight = lineCount * lineHeight + padding * 2;
            GUI.DrawTexture(new Rect(0, 0, _overlayPanelWidth, panelHeight), _bgTexture);

            if (_mainCamera != null)
            {
                Vector3 pos = _mainCamera.transform.position;
                _leftBuilder.Clear();
                _leftBuilder.Append("Pos: (");
                AppendFloat1(_leftBuilder, pos.x);
                _leftBuilder.Append(", ");
                AppendFloat1(_leftBuilder, pos.y);
                _leftBuilder.Append(", ");
                AppendFloat1(_leftBuilder, pos.z);
                _leftBuilder.Append(')');
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;
            }

            _leftBuilder.Clear();
            _leftBuilder.Append("FPS: ");
            AppendFloat1(_leftBuilder, _fps);
            GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
            y += lineHeight;

            if (_chunkManager != null)
            {
                _leftBuilder.Clear();
                _leftBuilder.Append("Chunks: ");
                _leftBuilder.Append(_chunkManager.LoadedCount);
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;
            }

            if (_chunkMeshStore != null)
            {
                _leftBuilder.Clear();
                _leftBuilder.Append("Renderers: ");
                _leftBuilder.Append(_chunkMeshStore.RendererCount);
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;

                _leftBuilder.Clear();
                _leftBuilder.Append("Cull: ");
                _leftBuilder.Append(_chunkMeshStore.IsOcclusionCullingActive ? "Frustum+Hi-Z" : "Frustum");
                long vramBytes =
                    (long)_chunkMeshStore.OpaqueBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.CutoutBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.TranslucentBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.OpaqueBuffer.IndexCapacity * 4
                    + (long)_chunkMeshStore.CutoutBuffer.IndexCapacity * 4
                    + (long)_chunkMeshStore.TranslucentBuffer.IndexCapacity * 4;
                _leftBuilder.Append("  VRAM: ");
                AppendBytes(_leftBuilder, vramBytes);
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;
            }

            if (_gameLoop != null)
            {
                _leftBuilder.Clear();
                _leftBuilder.Append("Gen Queue: ");
                _leftBuilder.Append(_gameLoop.PendingGenerationCount);
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;

                _leftBuilder.Clear();
                _leftBuilder.Append("Mesh Queue: ");
                _leftBuilder.Append(_gameLoop.PendingMeshCount);
                _leftBuilder.Append("  LOD Queue: ");
                _leftBuilder.Append(_gameLoop.PendingLODMeshCount);
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
                y += lineHeight;
            }

            if (_playerController != null && _playerController.IsFlying)
            {
                _leftBuilder.Clear();
                _leftBuilder.Append("FLY ");

                if (_playerController.IsNoclip)
                {
                    _leftBuilder.Append("[noclip] ");
                }

                AppendFloat1(_leftBuilder, _playerController.FlySpeed);
                _leftBuilder.Append(" b/s");
                GUI.Label(new Rect(x, y, labelWidth, lineHeight), _leftBuilder.ToString(), _style);
            }
        }

        private void DrawBenchmarkPanel()
        {
            int lineHeight = _smallStyle.fontSize + 6;
            int padding = _overlayPadding;
            int panelX = _overlayPanelWidth + padding;
            int colWidth = 340;
            int totalWidth = colWidth * 2 + padding * 3;
            int maxLines = 22;
            int panelHeight = maxLines * lineHeight + padding * 2;

            // Background
            GUI.DrawTexture(new Rect(panelX, 0, totalWidth, panelHeight), _bgTexture);

            int col1X = panelX + padding;
            int col2X = panelX + colWidth + padding * 2;
            int labelW = colWidth - padding;

            // --- Column 1: Pipeline Timing ---
            int y = padding;
            GUI.Label(new Rect(col1X, y, labelW, lineHeight), "-- Pipeline Timing --", _smallStyle);
            y += lineHeight;

            float maxBarMs = 5f; // Scale for bar width
            int barMaxWidth = 120;

            for (int i = 0; i < _displaySections.Length; i++)
            {
                int section = _displaySections[i];
                float ms = FrameProfiler.GetMs(section);

                _timingBuilder.Clear();
                _timingBuilder.Append(_paddedNames[i]);
                AppendMs(_timingBuilder, ms);

                GUI.Label(new Rect(col1X, y, 200, lineHeight), _timingBuilder.ToString(), _smallStyle);

                // Draw bar
                int barWidth = Mathf.Clamp(Mathf.RoundToInt((ms / maxBarMs) * barMaxWidth), 0, barMaxWidth);

                if (barWidth > 0)
                {
                    Color barColor = GetBarColor(ms);
                    Color prevColor = GUI.color;
                    GUI.color = barColor;
                    GUI.DrawTexture(new Rect(col1X + 200, y + 2, barWidth, lineHeight - 4), _barTexture);
                    GUI.color = prevColor;
                }

                y += lineHeight;
            }

            // Separator
            GUI.Label(new Rect(col1X, y, labelW, lineHeight), "------------------", _smallStyle);
            y += lineHeight;

            // Update total
            float updateMs = FrameProfiler.GetMs(FrameProfiler.UpdateTotal);
            float frameMs = _frameTimes[(_frameTimeHead - 1 + GraphSamples) % GraphSamples];
            float headroom = 16.667f - frameMs;

            _timingBuilder.Clear();
            _timingBuilder.Append("Update:     ");
            AppendMs(_timingBuilder, updateMs);
            GUI.Label(new Rect(col1X, y, labelW, lineHeight), _timingBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _timingBuilder.Clear();
            _timingBuilder.Append("Frame:      ");
            AppendMs(_timingBuilder, frameMs);
            GUI.Label(new Rect(col1X, y, labelW, lineHeight), _timingBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _timingBuilder.Clear();
            _timingBuilder.Append("Headroom:   ");
            AppendMs(_timingBuilder, headroom);
            _timingBuilder.Append(" (16.67 budget)");
            GUI.Label(new Rect(col1X, y, labelW, lineHeight), _timingBuilder.ToString(), _smallStyle);

            // --- Column 2: Pipeline Stats ---
            y = padding;

            GUI.Label(new Rect(col2X, y, labelW, lineHeight), "-- Throughput --", _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Gen:   ");
            _statsBuilder.Append(PipelineStats.GenCompleted);
            _statsBuilder.Append("/f  (total: ");
            _statsBuilder.Append(PipelineStats.TotalGenerated);
            _statsBuilder.Append(')');
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Mesh:  ");
            _statsBuilder.Append(PipelineStats.MeshCompleted);
            _statsBuilder.Append("/f  (total: ");
            _statsBuilder.Append(PipelineStats.TotalMeshed);
            _statsBuilder.Append(')');
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("LOD:   ");
            _statsBuilder.Append(PipelineStats.LODCompleted);
            _statsBuilder.Append("/f  (total: ");
            _statsBuilder.Append(PipelineStats.TotalLOD);
            _statsBuilder.Append(')');
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Decorate: ");
            _statsBuilder.Append(PipelineStats.DecorateCount);
            _statsBuilder.Append("x  ");
            AppendMs(_statsBuilder, PipelineStats.DecorateMs);
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            // GPU section
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), "-- GPU --", _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Upload: ");
            AppendBytes(_statsBuilder, PipelineStats.GpuUploadBytes);
            _statsBuilder.Append("/f  (");
            _statsBuilder.Append(PipelineStats.GpuUploadCount);
            _statsBuilder.Append(" calls)");
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Grow: ");
            _statsBuilder.Append(PipelineStats.GrowEvents);
            _statsBuilder.Append("  Free regions: ");
            _statsBuilder.Append(PipelineStats.FreeListSize);
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            // Buffer usage
            if (_chunkMeshStore != null)
            {
                DrawBufferLine(col2X, ref y, labelW, lineHeight, "Opaque",
                    _chunkMeshStore.OpaqueBuffer.UsedVertices,
                    _chunkMeshStore.OpaqueBuffer.VertexCapacity);
                DrawBufferLine(col2X, ref y, labelW, lineHeight, "Cutout",
                    _chunkMeshStore.CutoutBuffer.UsedVertices,
                    _chunkMeshStore.CutoutBuffer.VertexCapacity);
                DrawBufferLine(col2X, ref y, labelW, lineHeight, "Translucent",
                    _chunkMeshStore.TranslucentBuffer.UsedVertices,
                    _chunkMeshStore.TranslucentBuffer.VertexCapacity);
            }

            // Chunks section
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), "-- Chunks --", _smallStyle);
            y += lineHeight;

            // Show key states
            DrawChunkStateLine(col2X, ref y, labelW, lineHeight, "Generating",
                PipelineStats.StateHistogram[(int)ChunkState.Generating]);
            DrawChunkStateLine(col2X, ref y, labelW, lineHeight, "Generated",
                PipelineStats.StateHistogram[(int)ChunkState.Generated]);
            DrawChunkStateLine(col2X, ref y, labelW, lineHeight, "Meshing",
                PipelineStats.StateHistogram[(int)ChunkState.Meshing]);
            DrawChunkStateLine(col2X, ref y, labelW, lineHeight, "Ready",
                PipelineStats.StateHistogram[(int)ChunkState.Ready]);

            _statsBuilder.Clear();
            _statsBuilder.Append("NeedsRemesh: ");
            _statsBuilder.Append(PipelineStats.NeedsRemeshCount);
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("NeedsLight:  ");
            _statsBuilder.Append(PipelineStats.NeedsLightUpdateCount);
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
            y += lineHeight;

            _statsBuilder.Clear();
            _statsBuilder.Append("Pool: ");
            _statsBuilder.Append(PipelineStats.PoolAvailable);
            _statsBuilder.Append('/');
            _statsBuilder.Append(PipelineStats.PoolCheckedOut);
            _statsBuilder.Append('/');
            _statsBuilder.Append(PipelineStats.PoolTotal);
            GUI.Label(new Rect(col2X, y, labelW, lineHeight), _statsBuilder.ToString(), _smallStyle);
        }

        private void DrawBufferLine(int x, ref int y, int w, int h, string label, int used, int capacity)
        {
            _statsBuilder.Clear();
            _statsBuilder.Append(label);
            _statsBuilder.Append(": ");
            AppendVertCount(_statsBuilder, used);
            _statsBuilder.Append('/');
            AppendVertCount(_statsBuilder, capacity);
            _statsBuilder.Append(" verts");
            GUI.Label(new Rect(x, y, w, h), _statsBuilder.ToString(), _smallStyle);
            y += h;
        }

        private void DrawChunkStateLine(int x, ref int y, int w, int h, string label, int count)
        {
            _statsBuilder.Clear();
            _statsBuilder.Append(label);
            _statsBuilder.Append(": ");

            // Pad to align numbers
            int pad = 13 - label.Length;

            for (int i = 0; i < pad; i++)
            {
                _statsBuilder.Append(' ');
            }

            _statsBuilder.Append(count);
            GUI.Label(new Rect(x, y, w, h), _statsBuilder.ToString(), _smallStyle);
            y += h;
        }

        private static Color GetBarColor(float ms)
        {
            if (ms < 0.5f)
            {
                return _barGreen;
            }

            if (ms < 2f)
            {
                return _barYellow;
            }

            return _barRed;
        }

        /// <summary>
        /// Appends a float with 1 decimal place without string.Format allocation.
        /// </summary>
        private static void AppendFloat1(StringBuilder sb, float value)
        {
            if (value < 0f)
            {
                sb.Append('-');
                value = -value;
            }

            int whole = (int)value;
            int frac = (int)((value - whole) * 10f + 0.5f);

            if (frac >= 10)
            {
                whole++;
                frac -= 10;
            }

            sb.Append(whole);
            sb.Append('.');
            sb.Append(frac);
        }

        private static void AppendMs(StringBuilder sb, float ms)
        {
            if (ms < 0f)
            {
                sb.Append('-');
                ms = -ms;
            }

            // Manual formatting to avoid string.Format allocation
            int whole = (int)ms;
            int frac = (int)((ms - whole) * 100f + 0.5f);

            if (frac >= 100)
            {
                whole++;
                frac -= 100;
            }

            sb.Append(whole);
            sb.Append('.');

            if (frac < 10)
            {
                sb.Append('0');
            }

            sb.Append(frac);
            sb.Append("ms");
        }

        private static void AppendBytes(StringBuilder sb, long bytes)
        {
            if (bytes < 1024)
            {
                sb.Append(bytes);
                sb.Append(" B");
            }
            else if (bytes < 1024 * 1024)
            {
                sb.Append((bytes / 1024));
                sb.Append(" KB");
            }
            else
            {
                int mb = (int)(bytes / (1024 * 1024));
                int remainder = (int)((bytes % (1024 * 1024)) / 104858); // 1048576/10, single decimal digit
                sb.Append(mb);
                sb.Append('.');
                sb.Append(remainder);
                sb.Append(" MB");
            }
        }

        private static void AppendVertCount(StringBuilder sb, int count)
        {
            if (count < 1000)
            {
                sb.Append(count);
            }
            else if (count < 1000000)
            {
                sb.Append(count / 1000);
                sb.Append('.');
                sb.Append((count % 1000) / 100);
                sb.Append('K');
            }
            else
            {
                sb.Append(count / 1000000);
                sb.Append('.');
                sb.Append((count % 1000000) / 100000);
                sb.Append('M');
            }
        }

        private void DrawFrameTimeGraph()
        {
            float maxMs = 0f;
            float avgMs = 0f;

            for (int i = 0; i < _frameTimeFilled; i++)
            {
                float ms = _frameTimes[i];

                if (ms > maxMs)
                {
                    maxMs = ms;
                }

                avgMs += ms;
            }

            avgMs /= _frameTimeFilled;

            // Auto-scale Y axis to nearest useful ceiling
            float graphMaxMs;

            if (maxMs <= 20f)
            {
                graphMaxMs = 20f;
            }
            else if (maxMs <= 33.4f)
            {
                graphMaxMs = 34f;
            }
            else if (maxMs <= 50f)
            {
                graphMaxMs = 50f;
            }
            else if (maxMs <= 100f)
            {
                graphMaxMs = 100f;
            }
            else if (maxMs <= 200f)
            {
                graphMaxMs = 200f;
            }
            else
            {
                graphMaxMs = maxMs * 1.1f;
            }

            // Clear pixels to background
            for (int i = 0; i < _graphPixels.Length; i++)
            {
                _graphPixels[i] = ColorBg;
            }

            // 60fps threshold line (16.67ms)
            int line60Y = Mathf.Clamp(Mathf.RoundToInt((16.667f / graphMaxMs) * GraphHeight), 0, GraphHeight - 1);

            for (int px = 0; px < GraphWidth; px++)
            {
                _graphPixels[line60Y * GraphWidth + px] = ColorLine60;
            }

            // 30fps threshold line (33.33ms)
            int line30Y = Mathf.Clamp(Mathf.RoundToInt((33.333f / graphMaxMs) * GraphHeight), 0, GraphHeight - 1);

            if (line30Y < GraphHeight)
            {
                for (int px = 0; px < GraphWidth; px++)
                {
                    _graphPixels[line30Y * GraphWidth + px] = ColorLine30;
                }
            }

            // Draw bars: one pixel column per sample, most recent on the right
            int sampleCount = Mathf.Min(_frameTimeFilled, GraphSamples);

            for (int i = 0; i < sampleCount; i++)
            {
                int idx = (_frameTimeHead - sampleCount + i + GraphSamples) % GraphSamples;
                float ms = _frameTimes[idx];
                int barHeight = Mathf.Clamp(Mathf.RoundToInt((ms / graphMaxMs) * GraphHeight), 1, GraphHeight);

                Color32 barColor;

                if (ms < 16.667f)
                {
                    barColor = ColorGreen;
                }
                else if (ms < 33.333f)
                {
                    barColor = ColorYellow;
                }
                else
                {
                    barColor = ColorRed;
                }

                int colX = 1 + (GraphSamples - sampleCount) + i;

                if (colX < 0 || colX >= GraphWidth)
                {
                    continue;
                }

                for (int row = 0; row < barHeight; row++)
                {
                    _graphPixels[row * GraphWidth + colX] = barColor;
                }
            }

            _graphTexture.SetPixels32(_graphPixels);
            _graphTexture.Apply(false);

            // Position: bottom-left
            int graphPadding = _overlayPadding;
            int labelHeight = 20;
            float graphY = Screen.height - GraphHeight - graphPadding - labelHeight;

            GUI.DrawTexture(new Rect(graphPadding, graphY, GraphWidth, GraphHeight), _graphTexture);

            float currentMs = _frameTimes[(_frameTimeHead - 1 + GraphSamples) % GraphSamples];

            _leftBuilder.Clear();
            _leftBuilder.Append("Frame: ");
            AppendFloat1(_leftBuilder, currentMs);
            _leftBuilder.Append("ms  Max: ");
            AppendFloat1(_leftBuilder, maxMs);
            _leftBuilder.Append("ms  Avg: ");
            AppendFloat1(_leftBuilder, avgMs);
            _leftBuilder.Append("ms");

            GUI.Label(
                new Rect(graphPadding, graphY + GraphHeight + 2, GraphWidth + 100, labelHeight),
                _leftBuilder.ToString(), _smallStyle);
        }

        private void EnsureMinimapTexture()
        {
            int rd = _chunkManager.RenderDistance;
            int neededSize = rd * 2 + 1;

            if (_minimapTexture != null && _minimapSize == neededSize)
            {
                return;
            }

            if (_minimapTexture != null)
            {
                DestroyImmediate(_minimapTexture);
            }

            _minimapSize = neededSize;
            _minimapPixelScale = Mathf.Max(2, 150 / _minimapSize);
            _minimapTexture = new Texture2D(_minimapSize, _minimapSize, TextureFormat.RGBA32, false);
            _minimapTexture.filterMode = FilterMode.Point;
            _minimapTexture.wrapMode = TextureWrapMode.Clamp;
            _minimapPixels = new Color32[_minimapSize * _minimapSize];
        }

        private void UpdateMinimap()
        {
            if (_chunkManager == null || _minimapTexture == null || _mainCamera == null)
            {
                return;
            }

            int rd = _chunkManager.RenderDistance;
            Vector3 camPos = _mainCamera.transform.position;
            int camChunkX = Mathf.FloorToInt(camPos.x / ChunkConstants.Size);
            int camChunkY = Mathf.FloorToInt(camPos.y / ChunkConstants.Size);
            int camChunkZ = Mathf.FloorToInt(camPos.z / ChunkConstants.Size);

            for (int dx = -rd; dx <= rd; dx++)
            {
                for (int dz = -rd; dz <= rd; dz++)
                {
                    int3 coord = new int3(camChunkX + dx, camChunkY, camChunkZ + dz);
                    ManagedChunk chunk = _chunkManager.GetChunk(coord);

                    Color32 color;

                    if (chunk == null)
                    {
                        color = _stateColors[0];
                    }
                    else
                    {
                        int stateIdx = (int)chunk.State;

                        if (stateIdx >= 0 && stateIdx < _stateColors.Length)
                        {
                            color = _stateColors[stateIdx];
                        }
                        else
                        {
                            color = _stateColors[0];
                        }

                        if (chunk.NeedsRemesh)
                        {
                            color.r = (byte)Mathf.Min(255, color.r + 100);
                            color.g = (byte)(color.g / 2);
                            color.b = (byte)(color.b / 2);
                        }
                    }

                    int px = dx + rd;
                    int py = dz + rd;
                    _minimapPixels[py * _minimapSize + px] = color;
                }
            }

            // Camera center marker
            _minimapPixels[rd * _minimapSize + rd] = new Color32(255, 255, 255, 255);

            _minimapTexture.SetPixels32(_minimapPixels);
            _minimapTexture.Apply(false);
        }

        private void DrawMinimap()
        {
            int drawSize = _minimapSize * _minimapPixelScale;
            int padding = _overlayPadding;
            int labelHeight = 24;
            int legendHeight = _stateColors.Length * 18;
            int totalHeight = Mathf.Max(drawSize, legendHeight) + labelHeight;

            // Position: bottom-right, with enough room for label below
            float x = Screen.width - drawSize - padding;
            float y = Screen.height - totalHeight - padding;

            // Background for contrast
            GUI.DrawTexture(new Rect(x - 1, y - 1, drawSize + 2, drawSize + 2), _bgTexture);

            // Minimap texture scaled up with FilterMode.Point
            GUI.DrawTexture(new Rect(x, y, drawSize, drawSize), _minimapTexture);

            // Label below minimap
            _leftBuilder.Clear();
            _leftBuilder.Append("Chunks XZ (Y=");
            _leftBuilder.Append(Mathf.FloorToInt(_mainCamera.transform.position.y / ChunkConstants.Size));
            _leftBuilder.Append(") [");
            _leftBuilder.Append(_minimapSize);
            _leftBuilder.Append('x');
            _leftBuilder.Append(_minimapSize);
            _leftBuilder.Append(']');
            GUI.Label(new Rect(x, y + drawSize + 4, drawSize + 50, labelHeight), _leftBuilder.ToString(), _smallStyle);

            // Legend: to the left of the minimap
            DrawMinimapLegend(x - 80, y);
        }

        private void DrawMinimapLegend(float baseX, float baseY)
        {
            int boxSize = 8;
            int lineH = 18;
            int labelH = 20;

            for (int i = 0; i < _stateColors.Length; i++)
            {
                float ly = baseY + i * lineH;
                Color prev = GUI.color;
                GUI.color = _stateColors[i];
                GUI.DrawTexture(new Rect(baseX, ly + 4, boxSize, boxSize), _barTexture);
                GUI.color = prev;
                GUI.Label(new Rect(baseX + boxSize + 4, ly, 80, labelH), _stateNames[i], _smallStyle);
            }
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
            {
                DestroyImmediate(_bgTexture);
            }

            if (_graphTexture != null)
            {
                DestroyImmediate(_graphTexture);
            }

            if (_barTexture != null)
            {
                DestroyImmediate(_barTexture);
            }

            if (_minimapTexture != null)
            {
                DestroyImmediate(_minimapTexture);
            }
        }
    }
}
