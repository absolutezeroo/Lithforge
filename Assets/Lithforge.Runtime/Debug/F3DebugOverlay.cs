using System.Text;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Voxel.Chunk;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     F3-style debug overlay built with UI Toolkit. Replaces the old IMGUI DebugOverlayHUD.
    ///     Three-state cycle: Off → Minimal (FPS only) → Full (all panels + graph + minimap).
    ///     All labels use picking-mode: ignore. Most labels throttled to 4 Hz; FPS updates every frame.
    /// </summary>
    public sealed class F3DebugOverlay : MonoBehaviour
    {
        /// <summary>Minimum interval in seconds between throttled label updates (4 Hz).</summary>
        private const float ThrottleInterval = 0.25f;

        /// <summary>Profiler section indices displayed in the performance panel, excluding aggregates.</summary>
        private static readonly int[] s_displaySections =
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

        /// <summary>Right-padded section names for aligned display in the performance panel.</summary>
        private static readonly string[] s_paddedNames =
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

        /// <summary>Reusable StringBuilder for zero-allocation label text formatting.</summary>
        private readonly StringBuilder _sb = new(256);

        /// <summary>Reference to the chunk border wireframe renderer toggled by F3+G.</summary>
        private ChunkBorderRenderer _borderRenderer;

        /// <summary>Label displaying cutout render layer buffer usage (used/capacity).</summary>
        private Label _buffersCutoutLabel;

        /// <summary>Label displaying opaque render layer buffer usage (used/capacity).</summary>
        private Label _buffersOpaqueLabel;

        /// <summary>Label displaying translucent render layer buffer usage (used/capacity).</summary>
        private Label _buffersTransLabel;

        /// <summary>Label displaying the camera's current chunk coordinates.</summary>
        private Label _chunkLabel;

        /// <summary>Label displaying the total number of loaded chunks.</summary>
        private Label _chunksLoadedLabel;

        /// <summary>Label displaying the active culling mode and VRAM usage.</summary>
        private Label _cullModeLabel;

        /// <summary>Label displaying decoration pass count and timing.</summary>
        private Label _decorateLabel;

        /// <summary>UIDocument component hosting the overlay UI elements.</summary>
        private UIDocument _document;

        /// <summary>Forces an immediate update on the first frame after state change.</summary>
        private bool _firstUpdate = true;

        /// <summary>Label displaying fly mode status, noclip state, and speed.</summary>
        private Label _flyLabel;

        /// <summary>Label displaying the smoothed FPS counter (updated every frame).</summary>
        private Label _fpsLabel;

        /// <summary>Custom Painter2D element rendering the frame-time bar graph.</summary>
        private FrameTimeGraphElement _frameGraph;

        /// <summary>Label displaying the current frame time in milliseconds.</summary>
        private Label _frameMsLabel;

        /// <summary>Frame profiler instance for enabling profiling when overlay becomes visible.</summary>
        private IFrameProfiler _frameProfiler;

        /// <summary>Label displaying per-frame GC collection counts (gen0/gen1/gen2).</summary>
        private Label _gcLabel;

        /// <summary>Label displaying the pending generation queue depth.</summary>
        private Label _genQueueLabel;

        /// <summary>Label displaying generation throughput (per-frame and cumulative total).</summary>
        private Label _genThroughputLabel;

        /// <summary>Visual element panel containing GPU buffer and upload statistics.</summary>
        private VisualElement _gpuPanel;

        /// <summary>Label displaying GPU upload bytes and call count per frame.</summary>
        private Label _gpuUploadLabel;

        /// <summary>Label displaying frame-time graph statistics (current, max, avg).</summary>
        private Label _graphStatsLabel;

        /// <summary>Label displaying BufferArena grow events and arena count.</summary>
        private Label _growLabel;

        /// <summary>Label displaying frame budget headroom relative to 16.67ms target.</summary>
        private Label _headroomLabel;

        /// <summary>Left column container for performance and world panels.</summary>
        private VisualElement _leftColumn;

        /// <summary>Label displaying LOD mesh throughput (per-frame and cumulative total).</summary>
        private Label _lodThroughputLabel;

        /// <summary>Label displaying the pending mesh and LOD mesh queue depths.</summary>
        private Label _meshQueueLabel;

        /// <summary>Label displaying mesh throughput (per-frame and cumulative total).</summary>
        private Label _meshThroughputLabel;

        /// <summary>Metrics registry providing the current MetricSnapshot for display.</summary>
        private MetricsRegistry _metrics;

        /// <summary>Top-left panel visible in both Minimal and Full states, containing the FPS label.</summary>
        private VisualElement _minimalPanel;

        /// <summary>Custom Painter2D element rendering the chunk state minimap.</summary>
        private MinimapElement _minimap;

        /// <summary>Label below the minimap showing the current Y-level slice.</summary>
        private Label _minimapLabel;

        /// <summary>Label displaying the count of chunks pending light updates.</summary>
        private Label _needsLightLabel;

        /// <summary>Label displaying the count of chunks pending remeshing.</summary>
        private Label _needsRemeshLabel;

        /// <summary>Panel containing per-section profiler timings and frame budget info.</summary>
        private VisualElement _perfPanel;

        /// <summary>Panel containing pipeline queue depths, throughput, and chunk state histogram.</summary>
        private VisualElement _pipelinePanel;

        /// <summary>Pipeline stats instance for enabling stats when overlay becomes visible.</summary>
        private IPipelineStats _pipelineStats;

        /// <summary>Label displaying chunk pool statistics (available/checked-out/total).</summary>
        private Label _poolLabel;

        /// <summary>Label displaying the player's world position.</summary>
        private Label _posLabel;

        /// <summary>Label displaying the active chunk renderer count.</summary>
        private Label _renderersLabel;

        /// <summary>Right column container for pipeline and GPU panels.</summary>
        private VisualElement _rightColumn;

        /// <summary>Root visual element of the UIDocument.</summary>
        private VisualElement _root;

        /// <summary>Per-section profiler timing labels in the performance panel.</summary>
        private Label[] _sectionLabels;

        /// <summary>Whether chunk border wireframes are enabled via F3+G sub-toggle.</summary>
        private bool _showChunkBorders;

        /// <summary>Label displaying the chunk state histogram (Gen/Done/Mesh/Ready counts).</summary>
        private Label _stateHistLabel;

        /// <summary>Accumulator for throttling non-FPS label updates to 4 Hz.</summary>
        private float _throttleTimer;

        /// <summary>Label displaying the fixed tick count executed this frame.</summary>
        private Label _tickCountLabel;

        /// <summary>Label displaying the total Update() time in milliseconds.</summary>
        private Label _updateTotalLabel;

        /// <summary>Label displaying total VRAM usage across all GPU buffers.</summary>
        private Label _vramLabel;

        /// <summary>Panel containing world position, chunk info, and loaded chunk count.</summary>
        private VisualElement _worldPanel;

        /// <summary>Current three-state overlay mode (Off, Minimal, or Full).</summary>
        public OverlayState State { get; private set; } = OverlayState.Off;

        /// <summary>Updates FPS every frame and throttles all other labels to 4 Hz.</summary>
        private void Update()
        {
            HandleKeyInput();

            if (State == OverlayState.Off)
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

                if (State == OverlayState.Full)
                {
                    UpdateAllPanels(snap);
                }
            }

            // Frame-time graph and minimap update every frame
            if (State == OverlayState.Full)
            {
                _frameGraph.SetData(
                    _metrics.FrameTimeHistory,
                    _metrics.HistoryHead,
                    _metrics.HistoryFilled);
                _frameGraph.MarkDirtyRepaint();

                _minimap.MarkDirtyRepaint();
            }
        }

        /// <summary>Shows or hides the entire overlay root element.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Initializes the overlay with all dependencies, builds the UI, and applies the initial state.</summary>
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
                State = OverlayState.Full;
            }

            BuildUI();

            // Initialize minimap with live chunk data
            _minimap.Initialize(metrics.ChunkManager, metrics.MainCamera);

            ApplyState();
        }

        /// <summary>Constructs all visual elements, panels, labels, graph, and minimap.</summary>
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
            _leftColumn = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, left = 8, top = 36,
                },
            };
            _root.Add(_leftColumn);

            // Right column for full mode
            _rightColumn = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, right = 8, top = 8, alignItems = Align.FlexEnd,
                },
            };
            _root.Add(_rightColumn);

            BuildPerfPanel();
            BuildWorldPanel();
            BuildPipelinePanel();
            BuildGpuPanel();

            // Frame-time graph — bottom-left
            VisualElement graphContainer = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, left = 8, bottom = 8,
                },
            };

            _frameGraph = new FrameTimeGraphElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 302, height = 120,
                },
            };
            graphContainer.Add(_frameGraph);

            _graphStatsLabel = CreateLabel("");
            graphContainer.Add(_graphStatsLabel);

            _root.Add(graphContainer);

            // Minimap — bottom-right
            VisualElement minimapContainer = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, right = 8, bottom = 8, alignItems = Align.FlexEnd,
                },
            };

            _minimap = new MinimapElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 160, height = 160,
                },
            };
            minimapContainer.Add(_minimap);

            _minimapLabel = CreateLabel("");
            minimapContainer.Add(_minimapLabel);

            _root.Add(minimapContainer);
        }

        /// <summary>Builds the performance panel with frame time, section timings, headroom, and GC labels.</summary>
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

        /// <summary>Builds the world info panel with position, chunk, loaded count, and renderer labels.</summary>
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

        /// <summary>Builds the pipeline panel with queue depths, throughput, state histogram, and pool labels.</summary>
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

        /// <summary>Builds the GPU panel with VRAM, per-layer buffer usage, upload, and grow labels.</summary>
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

        /// <summary>Handles F3 key to cycle overlay state and F3+G to toggle chunk borders.</summary>
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
                State = (OverlayState)(((int)State + 1) % 3);
                ApplyState();

                // Enable profiling when entering any visible state
                if (State != OverlayState.Off)
                {
                    _frameProfiler.Enabled = true;
                    _pipelineStats.Enabled = true;
                }
            }
        }

        /// <summary>Shows or hides panels based on the current overlay state.</summary>
        private void ApplyState()
        {
            bool anyVisible = State != OverlayState.Off;
            bool full = State == OverlayState.Full;

            _minimalPanel.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _leftColumn.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _rightColumn.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _frameGraph.parent.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
            _minimap.parent.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;

            _firstUpdate = true;
        }

        /// <summary>Updates all full-mode panels from the current metric snapshot.</summary>
        private void UpdateAllPanels(MetricSnapshot snap)
        {
            UpdatePerfPanel(snap);
            UpdateWorldPanel(snap);
            UpdatePipelinePanel(snap);
            UpdateGpuPanel(snap);
            UpdateGraphStats(snap);
            UpdateMinimapLabel(snap);
        }

        /// <summary>Updates the performance panel labels with frame time, section timings, headroom, and GC data.</summary>
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

        /// <summary>Updates the world panel labels with player position, chunk info, and fly mode status.</summary>
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

        /// <summary>Updates the pipeline panel labels with queue depths, throughput, state histogram, and pool stats.</summary>
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

        /// <summary>Updates the GPU panel labels with VRAM, buffer usage, upload stats, and grow events.</summary>
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

        /// <summary>Updates the graph statistics label with current, max, and average frame times.</summary>
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

        /// <summary>Updates the minimap caption label with the current Y-level slice.</summary>
        private void UpdateMinimapLabel(MetricSnapshot snap)
        {
            _sb.Clear();
            _sb.Append("Chunks XZ (Y=");
            _sb.Append(snap.ChunkY);
            _sb.Append(')');
            _minimapLabel.text = _sb.ToString();
        }

        /// <summary>Creates a semi-transparent dark panel with rounded corners for grouping labels.</summary>
        private static VisualElement CreatePanel()
        {
            VisualElement panel = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    backgroundColor = new Color(0f, 0f, 0f, 0.65f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                },
            };
            return panel;
        }

        /// <summary>Creates a monospaced-style label with standard font size and light gray color.</summary>
        private static Label CreateLabel(string text)
        {
            Label label = new(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 13,
                    color = new Color(0.86f, 0.86f, 0.86f),
                    unityTextAlign = TextAnchor.UpperLeft,
                    marginTop = 1,
                    marginBottom = 1,
                    paddingTop = 0,
                    paddingBottom = 0,
                },
            };
            return label;
        }
    }
}
