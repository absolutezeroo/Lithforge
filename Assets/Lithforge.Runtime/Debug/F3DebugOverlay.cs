using System.Text;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Voxel.Chunk;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// F3-style debug overlay built with UI Toolkit. Replaces the old IMGUI DebugOverlayHUD.
    /// Three-state cycle: Off → Minimal (FPS only) → Full (all panels + graph + minimap).
    /// All labels use picking-mode: ignore. Most labels throttled to 4 Hz; FPS updates every frame.
    /// </summary>
    public sealed class F3DebugOverlay : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private MetricsRegistry _metrics;
        private ChunkBorderRenderer _borderRenderer;
        private IFrameProfiler _frameProfiler;
        private IPipelineStats _pipelineStats;

        // Three-state cycle
        private OverlayState _state = OverlayState.Off;

        // Panels
        private VisualElement _minimalPanel;
        private VisualElement _leftColumn;
        private VisualElement _rightColumn;
        private VisualElement _perfPanel;
        private VisualElement _worldPanel;
        private VisualElement _pipelinePanel;
        private VisualElement _gpuPanel;

        // Labels — pre-created, mutated in-place
        private Label _fpsLabel;
        private Label _frameMsLabel;
        private Label[] _sectionLabels;
        private Label _updateTotalLabel;
        private Label _headroomLabel;
        private Label _gcLabel;
        private Label _tickCountLabel;
        private Label _posLabel;
        private Label _chunkLabel;
        private Label _chunksLoadedLabel;
        private Label _renderersLabel;
        private Label _cullModeLabel;
        private Label _flyLabel;
        private Label _genQueueLabel;
        private Label _meshQueueLabel;
        private Label _genThroughputLabel;
        private Label _meshThroughputLabel;
        private Label _lodThroughputLabel;
        private Label _decorateLabel;
        private Label _stateHistLabel;
        private Label _needsRemeshLabel;
        private Label _needsLightLabel;
        private Label _poolLabel;
        private Label _vramLabel;
        private Label _buffersOpaqueLabel;
        private Label _buffersCutoutLabel;
        private Label _buffersTransLabel;
        private Label _gpuUploadLabel;
        private Label _growLabel;
        private Label _graphStatsLabel;
        private Label _minimapLabel;

        // Custom elements
        private FrameTimeGraphElement _frameGraph;
        private MinimapElement _minimap;

        // Throttle
        private const float ThrottleInterval = 0.25f;
        private float _throttleTimer;
        private bool _firstUpdate = true;

        // F3+G sub-toggle
        private bool _showChunkBorders;

        // Reusable StringBuilder
        private readonly StringBuilder _sb = new StringBuilder(256);

        // Display section indices (excludes UpdateTotal and Frame which are shown separately)
        private static readonly int[] s_displaySections = new int[]
        {
            FrameProfilerSections.TickLoop,
            FrameProfilerSections.PollGen,
            FrameProfilerSections.PollMesh,
            FrameProfilerSections.PollLOD,
            FrameProfilerSections.LoadQueue,
            FrameProfilerSections.Unload,
            FrameProfilerSections.SchedGen,
            FrameProfilerSections.CrossLight,
            FrameProfilerSections.Relight,
            FrameProfilerSections.LODLevels,
            FrameProfilerSections.SchedMesh,
            FrameProfilerSections.SchedLOD,
            FrameProfilerSections.Render,
        };

        private static readonly string[] s_paddedNames = new string[]
        {
            "TickLoop:   ",
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

        public OverlayState State
        {
            get { return _state; }
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Initialize(
            MetricsRegistry metrics,
            ChunkBorderRenderer borderRenderer,
            DebugSettings settings,
            PanelSettings panelSettings,
            IFrameProfiler frameProfiler,
            IPipelineStats pipelineStats)
        {
            _metrics = metrics;
            _borderRenderer = borderRenderer;
            _frameProfiler = frameProfiler;
            _pipelineStats = pipelineStats;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 150;

            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            if (settings.ShowDebugOverlay)
            {
                _state = OverlayState.Full;
            }

            BuildUI();

            // Initialize minimap with live chunk data
            _minimap.Initialize(metrics.ChunkManager, metrics.MainCamera);

            ApplyState();
        }

        private void BuildUI()
        {
            // Root fills the screen
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;

            // Minimal panel — top-left, FPS only
            _minimalPanel = CreatePanel();
            _minimalPanel.style.position = Position.Absolute;
            _minimalPanel.style.left = 8;
            _minimalPanel.style.top = 8;
            _fpsLabel = CreateLabel("FPS: --");
            _minimalPanel.Add(_fpsLabel);
            _root.Add(_minimalPanel);

            // Left column for full mode
            _leftColumn = new VisualElement();
            _leftColumn.pickingMode = PickingMode.Ignore;
            _leftColumn.style.position = Position.Absolute;
            _leftColumn.style.left = 8;
            _leftColumn.style.top = 36;
            _root.Add(_leftColumn);

            // Right column for full mode
            _rightColumn = new VisualElement();
            _rightColumn.pickingMode = PickingMode.Ignore;
            _rightColumn.style.position = Position.Absolute;
            _rightColumn.style.right = 8;
            _rightColumn.style.top = 8;
            _rightColumn.style.alignItems = Align.FlexEnd;
            _root.Add(_rightColumn);

            BuildPerfPanel();
            BuildWorldPanel();
            BuildPipelinePanel();
            BuildGpuPanel();

            // Frame-time graph — bottom-left
            VisualElement graphContainer = new VisualElement();
            graphContainer.pickingMode = PickingMode.Ignore;
            graphContainer.style.position = Position.Absolute;
            graphContainer.style.left = 8;
            graphContainer.style.bottom = 8;

            _frameGraph = new FrameTimeGraphElement();
            _frameGraph.pickingMode = PickingMode.Ignore;
            _frameGraph.style.width = 302;
            _frameGraph.style.height = 120;
            graphContainer.Add(_frameGraph);

            _graphStatsLabel = CreateLabel("");
            graphContainer.Add(_graphStatsLabel);

            _root.Add(graphContainer);

            // Minimap — bottom-right
            VisualElement minimapContainer = new VisualElement();
            minimapContainer.pickingMode = PickingMode.Ignore;
            minimapContainer.style.position = Position.Absolute;
            minimapContainer.style.right = 8;
            minimapContainer.style.bottom = 8;
            minimapContainer.style.alignItems = Align.FlexEnd;

            _minimap = new MinimapElement();
            _minimap.pickingMode = PickingMode.Ignore;
            _minimap.style.width = 160;
            _minimap.style.height = 160;
            minimapContainer.Add(_minimap);

            _minimapLabel = CreateLabel("");
            minimapContainer.Add(_minimapLabel);

            _root.Add(minimapContainer);
        }

        private void BuildPerfPanel()
        {
            _perfPanel = CreatePanel();
            Label header = CreateLabel("-- Performance --");
            header.style.color = new Color(0.4f, 0.8f, 1f);
            _perfPanel.Add(header);

            _frameMsLabel = CreateLabel("Frame: --");
            _perfPanel.Add(_frameMsLabel);

            _sectionLabels = new Label[s_displaySections.Length];

            for (int i = 0; i < s_displaySections.Length; i++)
            {
                _sectionLabels[i] = CreateLabel("");
                _perfPanel.Add(_sectionLabels[i]);
            }

            _updateTotalLabel = CreateLabel("Update: --");
            _perfPanel.Add(_updateTotalLabel);

            _headroomLabel = CreateLabel("Headroom: --");
            _perfPanel.Add(_headroomLabel);

            _gcLabel = CreateLabel("GC: --");
            _perfPanel.Add(_gcLabel);

            _tickCountLabel = CreateLabel("Ticks: --");
            _perfPanel.Add(_tickCountLabel);

            _leftColumn.Add(_perfPanel);
        }

        private void BuildWorldPanel()
        {
            _worldPanel = CreatePanel();
            _worldPanel.style.marginTop = 4;
            Label header = CreateLabel("-- World --");
            header.style.color = new Color(0.4f, 0.8f, 1f);
            _worldPanel.Add(header);

            _posLabel = CreateLabel("Pos: --");
            _worldPanel.Add(_posLabel);

            _chunkLabel = CreateLabel("Chunk: --");
            _worldPanel.Add(_chunkLabel);

            _chunksLoadedLabel = CreateLabel("Loaded: --");
            _worldPanel.Add(_chunksLoadedLabel);

            _renderersLabel = CreateLabel("Renderers: --");
            _worldPanel.Add(_renderersLabel);

            _cullModeLabel = CreateLabel("Cull: --");
            _worldPanel.Add(_cullModeLabel);

            _flyLabel = CreateLabel("");
            _worldPanel.Add(_flyLabel);

            _leftColumn.Add(_worldPanel);
        }

        private void BuildPipelinePanel()
        {
            _pipelinePanel = CreatePanel();
            Label header = CreateLabel("-- Pipeline --");
            header.style.color = new Color(0.4f, 0.8f, 1f);
            _pipelinePanel.Add(header);

            _genQueueLabel = CreateLabel("Gen Queue: --");
            _pipelinePanel.Add(_genQueueLabel);

            _meshQueueLabel = CreateLabel("Mesh Queue: --");
            _pipelinePanel.Add(_meshQueueLabel);

            _genThroughputLabel = CreateLabel("Gen: --");
            _pipelinePanel.Add(_genThroughputLabel);

            _meshThroughputLabel = CreateLabel("Mesh: --");
            _pipelinePanel.Add(_meshThroughputLabel);

            _lodThroughputLabel = CreateLabel("LOD: --");
            _pipelinePanel.Add(_lodThroughputLabel);

            _decorateLabel = CreateLabel("Decorate: --");
            _pipelinePanel.Add(_decorateLabel);

            _stateHistLabel = CreateLabel("States: --");
            _pipelinePanel.Add(_stateHistLabel);

            _needsRemeshLabel = CreateLabel("NeedsRemesh: --");
            _pipelinePanel.Add(_needsRemeshLabel);

            _needsLightLabel = CreateLabel("NeedsLight: --");
            _pipelinePanel.Add(_needsLightLabel);

            _poolLabel = CreateLabel("Pool: --");
            _pipelinePanel.Add(_poolLabel);

            _rightColumn.Add(_pipelinePanel);
        }

        private void BuildGpuPanel()
        {
            _gpuPanel = CreatePanel();
            _gpuPanel.style.marginTop = 4;
            Label header = CreateLabel("-- GPU --");
            header.style.color = new Color(0.4f, 0.8f, 1f);
            _gpuPanel.Add(header);

            _vramLabel = CreateLabel("VRAM: --");
            _gpuPanel.Add(_vramLabel);

            _buffersOpaqueLabel = CreateLabel("Opaque: --");
            _gpuPanel.Add(_buffersOpaqueLabel);

            _buffersCutoutLabel = CreateLabel("Cutout: --");
            _gpuPanel.Add(_buffersCutoutLabel);

            _buffersTransLabel = CreateLabel("Translucent: --");
            _gpuPanel.Add(_buffersTransLabel);

            _gpuUploadLabel = CreateLabel("Upload: --");
            _gpuPanel.Add(_gpuUploadLabel);

            _growLabel = CreateLabel("Grow: --");
            _gpuPanel.Add(_growLabel);

            _rightColumn.Add(_gpuPanel);
        }

        private void Update()
        {
            HandleKeyInput();

            if (_state == OverlayState.Off)
            {
                return;
            }

            MetricSnapshot snap = _metrics.CurrentSnapshot;

            // FPS updates every frame (not throttled)
            _sb.Clear();
            _sb.Append("FPS: ");
            DebugTextUtil.AppendInt(_sb, (int)(snap.FpsSmoothed + 0.5f));
            _fpsLabel.text = _sb.ToString();

            // All other labels throttled to 4 Hz
            _throttleTimer += Time.unscaledDeltaTime;

            if (_throttleTimer >= ThrottleInterval || _firstUpdate)
            {
                _throttleTimer = 0f;
                _firstUpdate = false;

                if (_state == OverlayState.Full)
                {
                    UpdateAllPanels(snap);
                }
            }

            // Frame-time graph and minimap update every frame
            if (_state == OverlayState.Full)
            {
                _frameGraph.SetData(
                    _metrics.FrameTimeHistory,
                    _metrics.HistoryHead,
                    _metrics.HistoryFilled);
                _frameGraph.MarkDirtyRepaint();

                _minimap.MarkDirtyRepaint();
            }
        }

        private void HandleKeyInput()
        {
            Keyboard kb = Keyboard.current;

            if (kb == null)
            {
                return;
            }

            // F3+G: toggle chunk borders (check G while F3 is held)
            if (kb.f3Key.isPressed && kb.gKey.wasPressedThisFrame)
            {
                _showChunkBorders = !_showChunkBorders;

                if (_borderRenderer != null)
                {
                    _borderRenderer.SetVisible(_showChunkBorders);
                }

                return;
            }

            // Plain F3: cycle state
            if (kb.f3Key.wasPressedThisFrame)
            {
                _state = (OverlayState)(((int)_state + 1) % 3);
                ApplyState();

                // Enable profiling when entering any visible state
                if (_state != OverlayState.Off)
                {
                    _frameProfiler.Enabled = true;
                    _pipelineStats.Enabled = true;
                }
            }
        }

        private void ApplyState()
        {
            bool anyVisible = _state != OverlayState.Off;
            bool full = _state == OverlayState.Full;

            _minimalPanel.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _leftColumn.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _rightColumn.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _frameGraph.parent.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _minimap.parent.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;

            _firstUpdate = true;
        }

        private void UpdateAllPanels(MetricSnapshot snap)
        {
            UpdatePerfPanel(snap);
            UpdateWorldPanel(snap);
            UpdatePipelinePanel(snap);
            UpdateGpuPanel(snap);
            UpdateGraphStats(snap);
            UpdateMinimapLabel(snap);
        }

        private void UpdatePerfPanel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("Frame: ");
            DebugTextUtil.AppendMs(_sb, snap.FrameMs);
            _frameMsLabel.text = _sb.ToString();

            for (int i = 0; i < s_displaySections.Length; i++)
            {
                int section = s_displaySections[i];
                float ms = snap.GetSectionMs(section);

                _sb.Clear();
                _sb.Append(s_paddedNames[i]);
                DebugTextUtil.AppendMs(_sb, ms);
                _sectionLabels[i].text = _sb.ToString();

                // Color-code: white normal, yellow >2ms, red >8ms
                if (ms >= 8f)
                {
                    _sectionLabels[i].style.color = new Color(0.9f, 0.2f, 0.2f);
                }
                else if (ms >= 2f)
                {
                    _sectionLabels[i].style.color = new Color(0.9f, 0.8f, 0.1f);
                }
                else
                {
                    _sectionLabels[i].style.color = new Color(0.86f, 0.86f, 0.86f);
                }
            }

            _sb.Clear();
            _sb.Append("Update:     ");
            DebugTextUtil.AppendMs(_sb, snap.GetSectionMs(FrameProfilerSections.UpdateTotal));
            _updateTotalLabel.text = _sb.ToString();

            float headroom = 16.667f - snap.FrameMs;
            _sb.Clear();
            _sb.Append("Headroom:   ");
            DebugTextUtil.AppendMs(_sb, headroom);
            _sb.Append(" (16.67 budget)");
            _headroomLabel.text = _sb.ToString();
            _headroomLabel.style.color = headroom >= 0f
                ? new Color(0f, 0.78f, 0f)
                : new Color(0.9f, 0.2f, 0.2f);

            _sb.Clear();
            _sb.Append("GC: ");
            _sb.Append(snap.GcGen0);
            _sb.Append('/');
            _sb.Append(snap.GcGen1);
            _sb.Append('/');
            _sb.Append(snap.GcGen2);
            _gcLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Ticks: ");
            _sb.Append(snap.TicksThisFrame);
            _sb.Append("/frame (30 TPS)");
            _tickCountLabel.text = _sb.ToString();
        }

        private void UpdateWorldPanel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("Pos: (");
            DebugTextUtil.AppendFloat1(_sb, snap.PlayerX);
            _sb.Append(", ");
            DebugTextUtil.AppendFloat1(_sb, snap.PlayerY);
            _sb.Append(", ");
            DebugTextUtil.AppendFloat1(_sb, snap.PlayerZ);
            _sb.Append(')');
            _posLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Chunk: (");
            _sb.Append(snap.ChunkX);
            _sb.Append(", ");
            _sb.Append(snap.ChunkY);
            _sb.Append(", ");
            _sb.Append(snap.ChunkZ);
            _sb.Append(')');
            _chunkLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Loaded: ");
            _sb.Append(snap.LoadedChunks);
            _chunksLoadedLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Renderers: ");
            _sb.Append(snap.RendererCount);
            _renderersLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Cull: ");
            _sb.Append(snap.OcclusionCullingActive ? "Frustum+Hi-Z" : "Frustum");
            _sb.Append("  VRAM: ");
            DebugTextUtil.AppendBytes(_sb, snap.VramTotalBytes);
            _cullModeLabel.text = _sb.ToString();

            if (snap.IsFlying)
            {
                _sb.Clear();
                _sb.Append("FLY ");

                if (snap.IsNoclip)
                {
                    _sb.Append("[noclip] ");
                }

                DebugTextUtil.AppendFloat1(_sb, snap.FlySpeed);
                _sb.Append(" b/s");
                _flyLabel.text = _sb.ToString();
                _flyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _flyLabel.style.display = DisplayStyle.None;
            }
        }

        private void UpdatePipelinePanel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("Gen Queue: ");
            _sb.Append(snap.PendingGenCount);
            _genQueueLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Mesh Queue: ");
            _sb.Append(snap.PendingMeshCount);
            _sb.Append("  LOD: ");
            _sb.Append(snap.PendingLodMeshCount);
            _meshQueueLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Gen:  ");
            _sb.Append(snap.GenCompleted);
            _sb.Append("/f  (total: ");
            _sb.Append(snap.TotalGenerated);
            _sb.Append(')');
            _genThroughputLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Mesh: ");
            _sb.Append(snap.MeshCompleted);
            _sb.Append("/f  (total: ");
            _sb.Append(snap.TotalMeshed);
            _sb.Append(')');
            _meshThroughputLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("LOD:  ");
            _sb.Append(snap.LodCompleted);
            _sb.Append("/f  (total: ");
            _sb.Append(snap.TotalLod);
            _sb.Append(')');
            _lodThroughputLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Decorate: ");
            _sb.Append(snap.DecorateCount);
            _sb.Append("x  ");
            DebugTextUtil.AppendMs(_sb, snap.DecorateMs);
            _decorateLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Gen:");
            _sb.Append(snap.GetChunkState((int)ChunkState.Generating));
            _sb.Append(" Done:");
            _sb.Append(snap.GetChunkState((int)ChunkState.Generated));
            _sb.Append(" Mesh:");
            _sb.Append(snap.GetChunkState((int)ChunkState.Meshing));
            _sb.Append(" Ready:");
            _sb.Append(snap.GetChunkState((int)ChunkState.Ready));
            _stateHistLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("NeedsRemesh: ");
            _sb.Append(snap.NeedsRemeshCount);
            _needsRemeshLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("NeedsLight:  ");
            _sb.Append(snap.NeedsLightUpdateCount);
            _needsLightLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Pool: ");
            _sb.Append(snap.PoolAvailable);
            _sb.Append('/');
            _sb.Append(snap.PoolCheckedOut);
            _sb.Append('/');
            _sb.Append(snap.PoolTotal);
            _poolLabel.text = _sb.ToString();
        }

        private void UpdateGpuPanel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("VRAM: ");
            DebugTextUtil.AppendBytes(_sb, snap.VramTotalBytes);
            _vramLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Opaque:      ");
            DebugTextUtil.AppendVertCount(_sb, snap.OpaqueUsedVerts);
            _sb.Append('/');
            DebugTextUtil.AppendVertCount(_sb, snap.OpaqueCapacityVerts);
            _buffersOpaqueLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Cutout:      ");
            DebugTextUtil.AppendVertCount(_sb, snap.CutoutUsedVerts);
            _sb.Append('/');
            DebugTextUtil.AppendVertCount(_sb, snap.CutoutCapacityVerts);
            _buffersCutoutLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Translucent: ");
            DebugTextUtil.AppendVertCount(_sb, snap.TranslucentUsedVerts);
            _sb.Append('/');
            DebugTextUtil.AppendVertCount(_sb, snap.TranslucentCapacityVerts);
            _buffersTransLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Upload: ");
            DebugTextUtil.AppendBytes(_sb, snap.GpuUploadBytes);
            _sb.Append("/f  (");
            _sb.Append(snap.GpuUploadCount);
            _sb.Append(" calls)");
            _gpuUploadLabel.text = _sb.ToString();

            _sb.Clear();
            _sb.Append("Grow: ");
            _sb.Append(snap.GrowEvents);
            _sb.Append("  Free: ");
            _sb.Append(snap.FreeListSize);
            _growLabel.text = _sb.ToString();
        }

        private void UpdateGraphStats(MetricSnapshot snap)
        {
            float maxMs = 0f;
            float avgMs = 0f;
            int filled = _metrics.HistoryFilled;
            float[] history = _metrics.FrameTimeHistory;

            for (int i = 0; i < filled; i++)
            {
                float ms = history[i];

                if (ms > maxMs)
                {
                    maxMs = ms;
                }

                avgMs += ms;
            }

            if (filled > 0)
            {
                avgMs /= filled;
            }

            _sb.Clear();
            _sb.Append("Frame: ");
            DebugTextUtil.AppendFloat1(_sb, snap.FrameMs);
            _sb.Append("ms  Max: ");
            DebugTextUtil.AppendFloat1(_sb, maxMs);
            _sb.Append("ms  Avg: ");
            DebugTextUtil.AppendFloat1(_sb, avgMs);
            _sb.Append("ms");
            _graphStatsLabel.text = _sb.ToString();
        }

        private void UpdateMinimapLabel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("Chunks XZ (Y=");
            _sb.Append(snap.ChunkY);
            _sb.Append(')');
            _minimapLabel.text = _sb.ToString();
        }

        private static VisualElement CreatePanel()
        {
            VisualElement panel = new VisualElement();
            panel.pickingMode = PickingMode.Ignore;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 4;
            panel.style.paddingBottom = 4;
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            return panel;
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = 13;
            label.style.color = new Color(0.86f, 0.86f, 0.86f);
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.marginTop = 1;
            label.style.marginBottom = 1;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            return label;
        }
    }
}
