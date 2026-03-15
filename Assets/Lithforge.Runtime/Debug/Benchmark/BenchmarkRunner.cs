using System;
using System.Collections;
using System.Text;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Coroutine-based benchmark runner that executes BenchmarkScenario assets.
    /// F5 opens a visual scenario picker. Arrow keys navigate, Enter runs, Escape closes.
    /// During measurement, per-frame data is recorded into pre-allocated parallel arrays.
    /// On completion, writes CSV + PNG reports and logs a summary.
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        private BenchmarkContext _context;
        private DebugSettings _settings;
        private MetricsRegistry _metrics;
        private PlayerController _playerController;

        private bool _running;
        private Coroutine _activeCoroutine;

        // Scenario selection
        private BenchmarkScenario[] _allScenarios;
        private int _selectedIndex;
        private bool _menuOpen;

        // UI
        private UIDocument _uiDocument;
        private VisualElement _uiRoot;
        private VisualElement _menuPanel;
        private Label _titleLabel;
        private Label[] _scenarioLabels;
        private Label _hintLabel;
        private VisualElement _statusPanel;
        private Label _statusLabel;
        private float _statusTimer;
        private const float StatusDuration = 3f;

        // Colors
        private static readonly Color s_selectedBg = new Color(0.15f, 0.4f, 0.7f, 0.9f);
        private static readonly Color s_normalBg = new Color(0f, 0f, 0f, 0f);
        private static readonly Color s_selectedText = new Color(1f, 1f, 1f);
        private static readonly Color s_normalText = new Color(0.78f, 0.78f, 0.78f);
        private static readonly Color s_titleColor = new Color(0.4f, 0.8f, 1f);
        private static readonly Color s_hintColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color s_runningColor = new Color(1f, 0.8f, 0.2f);

        // Pre-allocated parallel arrays for per-frame recording
        private int _capacity;
        private float[] _frameMs;
        private float[][] _sectionMs;
        private int[] _genCompleted;
        private int[] _meshCompleted;
        private int[] _lodCompleted;
        private long[] _gpuUploadBytes;
        private int[] _gpuUploadCount;
        private int[] _growEvents;
        private int[] _gcGen0;
        private int[] _gcGen1;
        private int[] _gcGen2;
        private int[] _genScheduled;
        private int[] _meshScheduled;
        private int[] _lodScheduled;
        private int[] _invalidateCount;
        private float[] _meshCompleteMaxMs;
        private int[] _meshCompleteStalls;
        private float[] _genCompleteMaxMs;
        private int[] _genCompleteStalls;
        private float[] _schedMeshFillMs;
        private float[] _schedMeshFilterMs;
        private float[] _schedMeshAllocMs;
        private float[] _schedMeshScheduleMs;
        private float[] _schedMeshFlushMs;
        private int[] _generatedSetSize;

        // Results display
        private string _lastSummary;
        private float _summaryDisplayTimer;
        private const float SummaryDisplayDuration = 15f;

        // Pre-allocated StringBuilder for summary
        private readonly StringBuilder _summaryBuilder = new StringBuilder(2048);

        public bool IsRunning
        {
            get { return _running; }
        }

        public string LastSummary
        {
            get { return _lastSummary; }
        }

        public float SummaryDisplayTimer
        {
            get { return _summaryDisplayTimer; }
        }

        public BenchmarkScenario SelectedScenario
        {
            get
            {
                if (_allScenarios == null || _allScenarios.Length == 0)
                {
                    return null;
                }

                return _allScenarios[_selectedIndex];
            }
        }

        public void Initialize(
            BenchmarkContext context,
            DebugSettings settings,
            MetricsRegistry metrics,
            PlayerController playerController,
            PanelSettings panelSettings)
        {
            _context = context;
            _settings = settings;
            _metrics = metrics;
            _playerController = playerController;

            // Pre-allocate for estimated max frames
            _capacity = 60000;
            AllocateArrays(_capacity);

            // Load all scenario assets from Resources/Settings/Benchmarks
            _allScenarios = Resources.LoadAll<BenchmarkScenario>("Settings/Benchmarks");

            // Sort alphabetically for consistent ordering
            Array.Sort(_allScenarios, (a, b) => string.Compare(a.ScenarioName, b.ScenarioName, StringComparison.Ordinal));

            // Find the default scenario index
            _selectedIndex = 0;
            BenchmarkScenario defaultScenario = _settings.DefaultBenchmarkScenario;

            if (defaultScenario != null && _allScenarios.Length > 0)
            {
                for (int i = 0; i < _allScenarios.Length; i++)
                {
                    if (_allScenarios[i] == defaultScenario)
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }

            BuildUI(panelSettings);

            if (_allScenarios.Length > 0)
            {
                UnityEngine.Debug.Log("[Benchmark] Loaded " + _allScenarios.Length +
                    " scenarios. Press F5 to open picker.");
            }
        }

        private void BuildUI(PanelSettings panelSettings)
        {
            GameObject uiGo = new GameObject("BenchmarkUI");
            uiGo.transform.SetParent(transform, false);
            _uiDocument = uiGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = panelSettings;
            _uiDocument.sortingOrder = 200;

            _uiRoot = _uiDocument.rootVisualElement;
            _uiRoot.pickingMode = PickingMode.Ignore;
            _uiRoot.style.position = Position.Absolute;
            _uiRoot.style.left = 0;
            _uiRoot.style.top = 0;
            _uiRoot.style.right = 0;
            _uiRoot.style.bottom = 0;

            BuildMenuPanel();
            BuildStatusPanel();

            // Start hidden
            _menuPanel.style.display = DisplayStyle.None;
            _statusPanel.style.display = DisplayStyle.None;
        }

        private void BuildMenuPanel()
        {
            // Centered container
            VisualElement centerContainer = new VisualElement();
            centerContainer.pickingMode = PickingMode.Ignore;
            centerContainer.style.position = Position.Absolute;
            centerContainer.style.left = 0;
            centerContainer.style.top = 0;
            centerContainer.style.right = 0;
            centerContainer.style.bottom = 0;
            centerContainer.style.justifyContent = Justify.Center;
            centerContainer.style.alignItems = Align.Center;
            _uiRoot.Add(centerContainer);

            // Menu panel
            _menuPanel = new VisualElement();
            _menuPanel.pickingMode = PickingMode.Ignore;
            _menuPanel.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.92f);
            _menuPanel.style.paddingLeft = 16;
            _menuPanel.style.paddingRight = 16;
            _menuPanel.style.paddingTop = 12;
            _menuPanel.style.paddingBottom = 12;
            _menuPanel.style.borderTopLeftRadius = 8;
            _menuPanel.style.borderTopRightRadius = 8;
            _menuPanel.style.borderBottomLeftRadius = 8;
            _menuPanel.style.borderBottomRightRadius = 8;
            _menuPanel.style.minWidth = 340;
            _menuPanel.style.borderLeftWidth = 1;
            _menuPanel.style.borderRightWidth = 1;
            _menuPanel.style.borderTopWidth = 1;
            _menuPanel.style.borderBottomWidth = 1;
            _menuPanel.style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
            _menuPanel.style.borderRightColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
            _menuPanel.style.borderTopColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
            _menuPanel.style.borderBottomColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
            centerContainer.Add(_menuPanel);

            // Title
            _titleLabel = CreateLabel("BENCHMARKS", 15, s_titleColor);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 8;
            _menuPanel.Add(_titleLabel);

            // Separator
            VisualElement sep = new VisualElement();
            sep.pickingMode = PickingMode.Ignore;
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
            sep.style.marginBottom = 6;
            _menuPanel.Add(sep);

            // Scenario rows
            if (_allScenarios != null && _allScenarios.Length > 0)
            {
                _scenarioLabels = new Label[_allScenarios.Length];

                for (int i = 0; i < _allScenarios.Length; i++)
                {
                    Label row = CreateLabel("  " + _allScenarios[i].ScenarioName, 14, s_normalText);
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.borderTopLeftRadius = 4;
                    row.style.borderTopRightRadius = 4;
                    row.style.borderBottomLeftRadius = 4;
                    row.style.borderBottomRightRadius = 4;
                    row.style.marginTop = 1;
                    row.style.marginBottom = 1;
                    _scenarioLabels[i] = row;
                    _menuPanel.Add(row);
                }
            }

            // Separator
            VisualElement sep2 = new VisualElement();
            sep2.pickingMode = PickingMode.Ignore;
            sep2.style.height = 1;
            sep2.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
            sep2.style.marginTop = 6;
            sep2.style.marginBottom = 4;
            _menuPanel.Add(sep2);

            // Hint
            _hintLabel = CreateLabel("\u2191\u2193 Select   Enter Run   Esc Close", 12, s_hintColor);
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _menuPanel.Add(_hintLabel);
        }

        private void BuildStatusPanel()
        {
            // Status toast — top-center
            VisualElement topContainer = new VisualElement();
            topContainer.pickingMode = PickingMode.Ignore;
            topContainer.style.position = Position.Absolute;
            topContainer.style.left = 0;
            topContainer.style.right = 0;
            topContainer.style.top = 16;
            topContainer.style.alignItems = Align.Center;
            _uiRoot.Add(topContainer);

            _statusPanel = new VisualElement();
            _statusPanel.pickingMode = PickingMode.Ignore;
            _statusPanel.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            _statusPanel.style.paddingLeft = 16;
            _statusPanel.style.paddingRight = 16;
            _statusPanel.style.paddingTop = 8;
            _statusPanel.style.paddingBottom = 8;
            _statusPanel.style.borderTopLeftRadius = 6;
            _statusPanel.style.borderTopRightRadius = 6;
            _statusPanel.style.borderBottomLeftRadius = 6;
            _statusPanel.style.borderBottomRightRadius = 6;
            topContainer.Add(_statusPanel);

            _statusLabel = CreateLabel("", 14, s_runningColor);
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusPanel.Add(_statusLabel);
        }

        private void ShowStatus(string message, Color color)
        {
            _statusLabel.text = message;
            _statusLabel.style.color = color;
            _statusPanel.style.display = DisplayStyle.Flex;
            _statusTimer = StatusDuration;
        }

        private void OpenMenu()
        {
            if (_allScenarios == null || _allScenarios.Length == 0)
            {
                return;
            }

            _menuOpen = true;
            _menuPanel.style.display = DisplayStyle.Flex;
            RefreshMenuHighlight();
        }

        private void CloseMenu()
        {
            _menuOpen = false;
            _menuPanel.style.display = DisplayStyle.None;
        }

        private void RefreshMenuHighlight()
        {
            if (_scenarioLabels == null)
            {
                return;
            }

            for (int i = 0; i < _scenarioLabels.Length; i++)
            {
                bool selected = i == _selectedIndex;
                _scenarioLabels[i].style.backgroundColor = selected ? s_selectedBg : s_normalBg;
                _scenarioLabels[i].style.color = selected ? s_selectedText : s_normalText;
                _scenarioLabels[i].text = (selected ? "\u25b6 " : "  ") + _allScenarios[i].ScenarioName;
            }
        }

        private static Label CreateLabel(string text, int fontSize, Color color)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            return label;
        }

        private void AllocateArrays(int capacity)
        {
            _frameMs = new float[capacity];
            _sectionMs = new float[FrameProfiler.SectionCount][];

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                _sectionMs[i] = new float[capacity];
            }

            _genCompleted = new int[capacity];
            _meshCompleted = new int[capacity];
            _lodCompleted = new int[capacity];
            _gpuUploadBytes = new long[capacity];
            _gpuUploadCount = new int[capacity];
            _growEvents = new int[capacity];
            _gcGen0 = new int[capacity];
            _gcGen1 = new int[capacity];
            _gcGen2 = new int[capacity];
            _genScheduled = new int[capacity];
            _meshScheduled = new int[capacity];
            _lodScheduled = new int[capacity];
            _invalidateCount = new int[capacity];
            _meshCompleteMaxMs = new float[capacity];
            _meshCompleteStalls = new int[capacity];
            _genCompleteMaxMs = new float[capacity];
            _genCompleteStalls = new int[capacity];
            _schedMeshFillMs = new float[capacity];
            _schedMeshFilterMs = new float[capacity];
            _schedMeshAllocMs = new float[capacity];
            _schedMeshScheduleMs = new float[capacity];
            _schedMeshFlushMs = new float[capacity];
            _generatedSetSize = new int[capacity];
        }

        private void Update()
        {
            // Countdown summary display timer
            if (_summaryDisplayTimer > 0f)
            {
                _summaryDisplayTimer -= Time.unscaledDeltaTime;
            }

            // Countdown status timer
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.unscaledDeltaTime;

                if (_statusTimer <= 0f)
                {
                    _statusPanel.style.display = DisplayStyle.None;
                }
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (_menuOpen)
            {
                HandleMenuInput(keyboard);
                return;
            }

            if (_running)
            {
                return;
            }

            // F5 to open menu
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                if (_context != null && _context.GameLoop != null && _context.GameLoop.SpawnReady)
                {
                    OpenMenu();
                }
            }
        }

        private void HandleMenuInput(Keyboard keyboard)
        {
            // Escape to close
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseMenu();
                return;
            }

            // F5 also closes
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                CloseMenu();
                return;
            }

            // Up arrow
            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                }
                else
                {
                    _selectedIndex = _allScenarios.Length - 1;
                }

                RefreshMenuHighlight();
                return;
            }

            // Down arrow
            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                if (_selectedIndex < _allScenarios.Length - 1)
                {
                    _selectedIndex++;
                }
                else
                {
                    _selectedIndex = 0;
                }

                RefreshMenuHighlight();
                return;
            }

            // Enter to run
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                BenchmarkScenario scenario = SelectedScenario;

                if (scenario != null)
                {
                    CloseMenu();
                    ShowStatus("Running: " + scenario.ScenarioName + "...", s_runningColor);
                    StartScenario(scenario);
                }
            }
        }

        public void StartScenario(BenchmarkScenario scenario)
        {
            if (_running)
            {
                UnityEngine.Debug.LogWarning("[Benchmark] Already running.");
                return;
            }

            _running = true;
            _lastSummary = null;
            _activeCoroutine = StartCoroutine(RunScenarioCoroutine(scenario));
        }

        private IEnumerator RunScenarioCoroutine(BenchmarkScenario scenario)
        {
            UnityEngine.Debug.Log("[Benchmark] Starting scenario: " + scenario.ScenarioName);

            // Enable profiling
            FrameProfiler.Enabled = true;
            PipelineStats.Enabled = true;

            // Disable tick-driven physics so benchmark commands can move the player directly.
            Tick.PlayerPhysicsBody physicsBody = _playerController != null
                ? _playerController.PhysicsBody : null;

            if (physicsBody != null)
            {
                physicsBody.ExternallyControlled = true;
            }

            int totalRecordedFrames = 0;
            BenchmarkPhase[] phases = scenario.Phases;

            if (phases == null || phases.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[Benchmark] Scenario has no phases.");
                FinishRun(null);
                yield break;
            }

            for (int p = 0; p < phases.Length; p++)
            {
                BenchmarkPhase phase = phases[p];
                UnityEngine.Debug.Log("[Benchmark] Phase " + (p + 1) + "/" + phases.Length +
                    ": " + phase.PhaseName);

                ShowStatus("Phase " + (p + 1) + "/" + phases.Length + ": " + phase.PhaseName, s_runningColor);

                // Execute all commands in the phase
                BenchmarkCommand[] commands = phase.Commands;

                if (commands != null)
                {
                    for (int c = 0; c < commands.Length; c++)
                    {
                        if (commands[c] != null)
                        {
                            IEnumerator exec = commands[c].Execute(_context);

                            while (exec.MoveNext())
                            {
                                yield return exec.Current;
                            }
                        }
                    }
                }

                // Warmup frames — just wait, don't record
                for (int w = 0; w < phase.WarmupFrames; w++)
                {
                    yield return null;
                }

                // Measurement frames — record per-frame data
                // Ensure capacity
                int needed = totalRecordedFrames + phase.MeasurementFrames;

                if (needed > _capacity)
                {
                    int newCapacity = Mathf.Max(needed, _capacity * 2);
                    GrowArrays(newCapacity);
                }

                for (int m = 0; m < phase.MeasurementFrames; m++)
                {
                    yield return null;

                    int f = totalRecordedFrames;
                    MetricSnapshot snap = _metrics.CurrentSnapshot;

                    _frameMs[f] = snap.FrameMs;

                    for (int i = 0; i < FrameProfiler.SectionCount; i++)
                    {
                        _sectionMs[i][f] = snap.GetSectionMs(i);
                    }

                    _genCompleted[f] = snap.GenCompleted;
                    _meshCompleted[f] = snap.MeshCompleted;
                    _lodCompleted[f] = snap.LodCompleted;
                    _gpuUploadBytes[f] = snap.GpuUploadBytes;
                    _gpuUploadCount[f] = snap.GpuUploadCount;
                    _growEvents[f] = snap.GrowEvents;
                    _gcGen0[f] = snap.GcGen0;
                    _gcGen1[f] = snap.GcGen1;
                    _gcGen2[f] = snap.GcGen2;
                    _genScheduled[f] = snap.GenScheduled;
                    _meshScheduled[f] = snap.MeshScheduled;
                    _lodScheduled[f] = snap.LodScheduled;
                    _invalidateCount[f] = snap.InvalidateCount;
                    _meshCompleteMaxMs[f] = snap.MeshCompleteMaxMs;
                    _meshCompleteStalls[f] = snap.MeshCompleteStalls;
                    _genCompleteMaxMs[f] = snap.GenCompleteMaxMs;
                    _genCompleteStalls[f] = snap.GenCompleteStalls;
                    _schedMeshFillMs[f] = snap.SchedMeshFillMs;
                    _schedMeshFilterMs[f] = snap.SchedMeshFilterMs;
                    _schedMeshAllocMs[f] = snap.SchedMeshAllocMs;
                    _schedMeshScheduleMs[f] = snap.SchedMeshScheduleMs;
                    _schedMeshFlushMs[f] = snap.SchedMeshFlushMs;
                    _generatedSetSize[f] = snap.GeneratedSetSize;

                    totalRecordedFrames++;
                }
            }

            // Re-enable tick-driven physics and sync position back
            if (physicsBody != null)
            {
                Unity.Mathematics.float3 finalPos = new Unity.Mathematics.float3(
                    _context.PlayerTransform.position.x,
                    _context.PlayerTransform.position.y,
                    _context.PlayerTransform.position.z);
                physicsBody.Teleport(finalPos);
                physicsBody.ExternallyControlled = false;
            }

            // Build result
            BenchmarkResult result = BuildResult(scenario, totalRecordedFrames);

            // Write outputs
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string outputDir = Application.persistentDataPath;
            BenchmarkCsvWriter.Write(result, outputDir, timestamp);

            // Build and display summary
            string summary = BuildSummary(result);
            _lastSummary = summary;
            _summaryDisplayTimer = SummaryDisplayDuration;
            UnityEngine.Debug.Log(summary);

            // Show completion status
            string passText = result.Passed ? "PASS" : "FAIL";
            Color passColor = result.Passed ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            ShowStatus(scenario.ScenarioName + " — " + passText +
                "  (avg " + result.AvgFrameMs.ToString("F1") + "ms, " +
                result.AvgFps.ToString("F0") + " FPS)", passColor);
            _statusTimer = 8f;

            _running = false;
            _activeCoroutine = null;
        }

        private void GrowArrays(int newCapacity)
        {
            float[] newFrameMs = new float[newCapacity];
            Array.Copy(_frameMs, newFrameMs, _capacity);
            _frameMs = newFrameMs;

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                float[] newSection = new float[newCapacity];
                Array.Copy(_sectionMs[i], newSection, _capacity);
                _sectionMs[i] = newSection;
            }

            int[] newGenCompleted = new int[newCapacity];
            Array.Copy(_genCompleted, newGenCompleted, _capacity);
            _genCompleted = newGenCompleted;

            int[] newMeshCompleted = new int[newCapacity];
            Array.Copy(_meshCompleted, newMeshCompleted, _capacity);
            _meshCompleted = newMeshCompleted;

            int[] newLodCompleted = new int[newCapacity];
            Array.Copy(_lodCompleted, newLodCompleted, _capacity);
            _lodCompleted = newLodCompleted;

            long[] newGpuUploadBytes = new long[newCapacity];
            Array.Copy(_gpuUploadBytes, newGpuUploadBytes, _capacity);
            _gpuUploadBytes = newGpuUploadBytes;

            int[] newGpuUploadCount = new int[newCapacity];
            Array.Copy(_gpuUploadCount, newGpuUploadCount, _capacity);
            _gpuUploadCount = newGpuUploadCount;

            int[] newGrowEvents = new int[newCapacity];
            Array.Copy(_growEvents, newGrowEvents, _capacity);
            _growEvents = newGrowEvents;

            int[] newGcGen0 = new int[newCapacity];
            Array.Copy(_gcGen0, newGcGen0, _capacity);
            _gcGen0 = newGcGen0;

            int[] newGcGen1 = new int[newCapacity];
            Array.Copy(_gcGen1, newGcGen1, _capacity);
            _gcGen1 = newGcGen1;

            int[] newGcGen2 = new int[newCapacity];
            Array.Copy(_gcGen2, newGcGen2, _capacity);
            _gcGen2 = newGcGen2;

            int[] newGenScheduled = new int[newCapacity];
            Array.Copy(_genScheduled, newGenScheduled, _capacity);
            _genScheduled = newGenScheduled;

            int[] newMeshScheduled = new int[newCapacity];
            Array.Copy(_meshScheduled, newMeshScheduled, _capacity);
            _meshScheduled = newMeshScheduled;

            int[] newLodScheduled = new int[newCapacity];
            Array.Copy(_lodScheduled, newLodScheduled, _capacity);
            _lodScheduled = newLodScheduled;

            int[] newInvalidateCount = new int[newCapacity];
            Array.Copy(_invalidateCount, newInvalidateCount, _capacity);
            _invalidateCount = newInvalidateCount;

            float[] newMeshCompleteMaxMs = new float[newCapacity];
            Array.Copy(_meshCompleteMaxMs, newMeshCompleteMaxMs, _capacity);
            _meshCompleteMaxMs = newMeshCompleteMaxMs;

            int[] newMeshCompleteStalls = new int[newCapacity];
            Array.Copy(_meshCompleteStalls, newMeshCompleteStalls, _capacity);
            _meshCompleteStalls = newMeshCompleteStalls;

            float[] newGenCompleteMaxMs = new float[newCapacity];
            Array.Copy(_genCompleteMaxMs, newGenCompleteMaxMs, _capacity);
            _genCompleteMaxMs = newGenCompleteMaxMs;

            int[] newGenCompleteStalls = new int[newCapacity];
            Array.Copy(_genCompleteStalls, newGenCompleteStalls, _capacity);
            _genCompleteStalls = newGenCompleteStalls;

            float[] newSchedMeshFillMs = new float[newCapacity];
            Array.Copy(_schedMeshFillMs, newSchedMeshFillMs, _capacity);
            _schedMeshFillMs = newSchedMeshFillMs;

            float[] newSchedMeshFilterMs = new float[newCapacity];
            Array.Copy(_schedMeshFilterMs, newSchedMeshFilterMs, _capacity);
            _schedMeshFilterMs = newSchedMeshFilterMs;

            float[] newSchedMeshAllocMs = new float[newCapacity];
            Array.Copy(_schedMeshAllocMs, newSchedMeshAllocMs, _capacity);
            _schedMeshAllocMs = newSchedMeshAllocMs;

            float[] newSchedMeshScheduleMs = new float[newCapacity];
            Array.Copy(_schedMeshScheduleMs, newSchedMeshScheduleMs, _capacity);
            _schedMeshScheduleMs = newSchedMeshScheduleMs;

            float[] newSchedMeshFlushMs = new float[newCapacity];
            Array.Copy(_schedMeshFlushMs, newSchedMeshFlushMs, _capacity);
            _schedMeshFlushMs = newSchedMeshFlushMs;

            int[] newGeneratedSetSize = new int[newCapacity];
            Array.Copy(_generatedSetSize, newGeneratedSetSize, _capacity);
            _generatedSetSize = newGeneratedSetSize;

            _capacity = newCapacity;
        }

        private BenchmarkResult BuildResult(BenchmarkScenario scenario, int totalFrames)
        {
            BenchmarkResult result = new BenchmarkResult();
            result.ScenarioName = scenario.ScenarioName;
            result.TotalFrames = totalFrames;

            // Assign array references
            result.FrameMs = _frameMs;
            result.SectionMs = _sectionMs;
            result.GenCompleted = _genCompleted;
            result.MeshCompleted = _meshCompleted;
            result.LodCompleted = _lodCompleted;
            result.GpuUploadBytes = _gpuUploadBytes;
            result.GpuUploadCount = _gpuUploadCount;
            result.GrowEvents = _growEvents;
            result.GcGen0 = _gcGen0;
            result.GcGen1 = _gcGen1;
            result.GcGen2 = _gcGen2;
            result.GenScheduled = _genScheduled;
            result.MeshScheduled = _meshScheduled;
            result.LodScheduled = _lodScheduled;
            result.InvalidateCount = _invalidateCount;
            result.MeshCompleteMaxMs = _meshCompleteMaxMs;
            result.MeshCompleteStalls = _meshCompleteStalls;
            result.GenCompleteMaxMs = _genCompleteMaxMs;
            result.GenCompleteStalls = _genCompleteStalls;
            result.SchedMeshFillMs = _schedMeshFillMs;
            result.SchedMeshFilterMs = _schedMeshFilterMs;
            result.SchedMeshAllocMs = _schedMeshAllocMs;
            result.SchedMeshScheduleMs = _schedMeshScheduleMs;
            result.SchedMeshFlushMs = _schedMeshFlushMs;
            result.GeneratedSetSize = _generatedSetSize;

            if (totalFrames == 0)
            {
                return result;
            }

            // Compute timing stats
            float totalMs = 0f;
            float minMs = float.MaxValue;
            float maxMs = 0f;

            for (int i = 0; i < totalFrames; i++)
            {
                float ms = _frameMs[i];
                totalMs += ms;

                if (ms < minMs)
                {
                    minMs = ms;
                }

                if (ms > maxMs)
                {
                    maxMs = ms;
                }
            }

            float avgMs = Mathf.Max(totalMs / totalFrames, 0.001f);
            result.DurationSeconds = totalMs / 1000f;
            result.AvgFrameMs = avgMs;
            result.MinFrameMs = minMs;
            result.MaxFrameMs = maxMs;

            // Percentiles (sorted copy)
            float[] sortedMs = new float[totalFrames];
            Array.Copy(_frameMs, sortedMs, totalFrames);
            Array.Sort(sortedMs);

            result.P1FrameMs = Mathf.Max(sortedMs[(int)(totalFrames * 0.01f)], 0.001f);
            result.P99FrameMs = Mathf.Max(
                sortedMs[Math.Min((int)(totalFrames * 0.99f), totalFrames - 1)], 0.001f);

            // FPS stats
            result.AvgFps = 1000f / avgMs;
            result.MinFps = 1000f / Mathf.Max(maxMs, 0.001f);
            result.MaxFps = 1000f / Mathf.Max(minMs, 0.001f);
            result.P1Fps = 1000f / result.P99FrameMs;
            result.P99Fps = 1000f / result.P1FrameMs;

            // Pipeline totals
            int totalGen = 0;
            int totalMesh = 0;
            long totalGpuBytes = 0;
            int totalGrow = 0;

            for (int f = 0; f < totalFrames; f++)
            {
                totalGen += _genCompleted[f];
                totalMesh += _meshCompleted[f];
                totalGpuBytes += _gpuUploadBytes[f];
                totalGrow += _growEvents[f];
            }

            result.TotalGenerated = totalGen;
            result.TotalMeshed = totalMesh;
            result.TotalGpuUploadBytes = totalGpuBytes;
            result.TotalGrowEvents = totalGrow;

            // Section averages and top costs
            float[] sectionAvg = new float[FrameProfiler.SectionCount];

            for (int s = 0; s < FrameProfiler.SectionCount; s++)
            {
                float sum = 0f;

                for (int f = 0; f < totalFrames; f++)
                {
                    sum += _sectionMs[s][f];
                }

                sectionAvg[s] = sum / totalFrames;
            }

            // Sort section indices by avg ms descending
            int[] sectionOrder = new int[FrameProfiler.SectionCount];

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                sectionOrder[i] = i;
            }

            for (int i = 1; i < sectionOrder.Length; i++)
            {
                int key = sectionOrder[i];
                int j = i - 1;

                while (j >= 0 && sectionAvg[sectionOrder[j]] < sectionAvg[key])
                {
                    sectionOrder[j + 1] = sectionOrder[j];
                    j--;
                }

                sectionOrder[j + 1] = key;
            }

            // Top 4 non-aggregate sections
            int[] topIndices = new int[4];
            float[] topAvgs = new float[4];
            int filled = 0;

            for (int i = 0; i < sectionOrder.Length && filled < 4; i++)
            {
                int s = sectionOrder[i];

                if (s == FrameProfiler.UpdateTotal || s == FrameProfiler.Frame)
                {
                    continue;
                }

                topIndices[filled] = s;
                topAvgs[filled] = sectionAvg[s];
                filled++;
            }

            result.TopSectionIndices = topIndices;
            result.TopSectionAvgMs = topAvgs;

            // Bottleneck detection
            float budgetMs = 16.667f;
            result.BottleneckDescription = "None detected";

            for (int i = 0; i < sectionOrder.Length; i++)
            {
                int s = sectionOrder[i];

                if (s == FrameProfiler.UpdateTotal || s == FrameProfiler.Frame)
                {
                    continue;
                }

                float pct = (sectionAvg[s] / budgetMs) * 100f;

                if (pct >= 15f)
                {
                    result.BottleneckDescription = FrameProfiler.SectionNames[s] + " (" +
                        sectionAvg[s].ToString("F1") + "ms, " +
                        pct.ToString("F1") + "% of frame)";
                    break;
                }
            }

            // Pass/fail
            result.MaxAvgFrameTimeMs = scenario.MaxAvgFrameTimeMs;
            result.Passed = avgMs <= scenario.MaxAvgFrameTimeMs;

            return result;
        }

        private string BuildSummary(BenchmarkResult result)
        {
            if (result.TotalFrames == 0)
            {
                return "[Benchmark] No frames recorded.";
            }

            _summaryBuilder.Clear();
            _summaryBuilder.AppendLine("=== BENCHMARK RESULTS ===");
            _summaryBuilder.Append("Scenario:  ");
            _summaryBuilder.AppendLine(result.ScenarioName);

            _summaryBuilder.Append("Duration:  ");
            _summaryBuilder.Append(result.DurationSeconds.ToString("F1"));
            _summaryBuilder.Append("s  |  Frames: ");
            _summaryBuilder.AppendLine(result.TotalFrames.ToString());

            _summaryBuilder.Append("FPS:       avg ");
            _summaryBuilder.Append(result.AvgFps.ToString("F1"));
            _summaryBuilder.Append("  min ");
            _summaryBuilder.Append(result.MinFps.ToString("F0"));
            _summaryBuilder.Append("  max ");
            _summaryBuilder.Append(result.MaxFps.ToString("F0"));
            _summaryBuilder.Append("  p1 ");
            _summaryBuilder.Append(result.P1Fps.ToString("F0"));
            _summaryBuilder.Append("  p99 ");
            _summaryBuilder.AppendLine(result.P99Fps.ToString("F0"));

            _summaryBuilder.Append("Frame:     avg ");
            _summaryBuilder.Append(result.AvgFrameMs.ToString("F1"));
            _summaryBuilder.Append("ms  max ");
            _summaryBuilder.Append(result.MaxFrameMs.ToString("F1"));
            _summaryBuilder.Append("ms  p99 ");
            _summaryBuilder.Append(result.P99FrameMs.ToString("F1"));
            _summaryBuilder.AppendLine("ms");

            _summaryBuilder.AppendLine("--- Top costs (avg ms) ---");

            for (int i = 0; i < result.TopSectionIndices.Length; i++)
            {
                int s = result.TopSectionIndices[i];
                float avg = result.TopSectionAvgMs[i];

                if (avg < 0.001f)
                {
                    break;
                }

                float pct = (avg / result.AvgFrameMs) * 100f;
                _summaryBuilder.Append(FrameProfiler.SectionNames[s]);
                _summaryBuilder.Append(":  ");
                _summaryBuilder.Append(avg.ToString("F1"));
                _summaryBuilder.Append("ms (");
                _summaryBuilder.Append(pct.ToString("F1"));
                _summaryBuilder.AppendLine("%)");
            }

            float durationSec = Mathf.Max(result.DurationSeconds, 0.001f);
            _summaryBuilder.AppendLine("--- Pipeline ---");
            _summaryBuilder.Append("Generated:  ");
            _summaryBuilder.Append(result.TotalGenerated);
            _summaryBuilder.Append(" chunks  (");
            _summaryBuilder.Append((result.TotalGenerated / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine("/s)");

            _summaryBuilder.Append("Meshed:     ");
            _summaryBuilder.Append(result.TotalMeshed);
            _summaryBuilder.Append(" chunks  (");
            _summaryBuilder.Append((result.TotalMeshed / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine("/s)");

            float gpuMb = result.TotalGpuUploadBytes / (1024f * 1024f);
            _summaryBuilder.Append("GPU Upload: ");
            _summaryBuilder.Append(gpuMb.ToString("F1"));
            _summaryBuilder.Append(" MB total  (");
            _summaryBuilder.Append((gpuMb / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine(" MB/s)");

            _summaryBuilder.Append("Grow events: ");
            _summaryBuilder.AppendLine(result.TotalGrowEvents.ToString());

            _summaryBuilder.AppendLine("--- Bottleneck ---");
            _summaryBuilder.AppendLine(result.BottleneckDescription);

            _summaryBuilder.Append("--- Result: ");
            _summaryBuilder.Append(result.Passed ? "PASS" : "FAIL");
            _summaryBuilder.Append(" (threshold: ");
            _summaryBuilder.Append(result.MaxAvgFrameTimeMs.ToString("F1"));
            _summaryBuilder.AppendLine("ms avg) ---");

            _summaryBuilder.Append("=========================");

            return _summaryBuilder.ToString();
        }

        /// <summary>
        /// Safety net: if the MonoBehaviour is disabled mid-benchmark (e.g. scene change),
        /// ensure ExternallyControlled is cleared so the player isn't permanently frozen.
        /// </summary>
        private void OnDisable()
        {
            if (_running)
            {
                _running = false;
                _activeCoroutine = null;

                Tick.PlayerPhysicsBody physicsBody = _playerController != null
                    ? _playerController.PhysicsBody : null;

                if (physicsBody != null)
                {
                    physicsBody.ExternallyControlled = false;
                }
            }
        }

        private void FinishRun(BenchmarkResult result)
        {
            _running = false;
            _activeCoroutine = null;

            Tick.PlayerPhysicsBody physicsBody = _playerController != null
                ? _playerController.PhysicsBody : null;

            if (physicsBody != null)
            {
                physicsBody.ExternallyControlled = false;
            }
        }
    }
}
