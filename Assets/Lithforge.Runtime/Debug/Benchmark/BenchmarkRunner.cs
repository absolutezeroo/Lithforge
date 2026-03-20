using System;
using System.Collections;
using System.Text;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Session;
using Lithforge.Runtime.Tick;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    ///     Coroutine-based benchmark runner that executes BenchmarkScenario assets.
    ///     F5 opens a visual scenario picker. Arrow keys navigate, Enter runs, Escape closes.
    ///     During measurement, per-frame data is recorded into pre-allocated parallel arrays.
    ///     On completion, writes CSV + PNG reports and logs a summary.
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        /// <summary>Duration in seconds to show the status toast before it auto-hides.</summary>
        private const float StatusDuration = 3f;

        /// <summary>Duration in seconds to display the benchmark summary after completion.</summary>
        private const float SummaryDisplayDuration = 15f;

        /// <summary>Background color for the currently selected scenario row.</summary>
        private static readonly Color s_selectedBg = new(0.15f, 0.4f, 0.7f, 0.9f);

        /// <summary>Background color for unselected scenario rows (transparent).</summary>
        private static readonly Color s_normalBg = new(0f, 0f, 0f, 0f);

        /// <summary>Text color for the currently selected scenario row.</summary>
        private static readonly Color s_selectedText = new(1f, 1f, 1f);

        /// <summary>Text color for unselected scenario rows.</summary>
        private static readonly Color s_normalText = new(0.78f, 0.78f, 0.78f);

        /// <summary>Text color for the menu title label.</summary>
        private static readonly Color s_titleColor = new(0.4f, 0.8f, 1f);

        /// <summary>Text color for the keyboard hint label at the bottom of the menu.</summary>
        private static readonly Color s_hintColor = new(0.5f, 0.5f, 0.5f);

        /// <summary>Text color for the running status toast.</summary>
        private static readonly Color s_runningColor = new(1f, 0.8f, 0.2f);

        /// <summary>Pre-allocated StringBuilder reused for building benchmark summary text.</summary>
        private readonly StringBuilder _summaryBuilder = new(2048);

        /// <summary>Reference to the currently running benchmark coroutine, or null if idle.</summary>
        private Coroutine _activeCoroutine;

        /// <summary>All BenchmarkScenario assets loaded from Resources, sorted alphabetically.</summary>
        private BenchmarkScenario[] _allScenarios;

        /// <summary>Current capacity of the pre-allocated per-frame recording arrays.</summary>
        private int _capacity;

        /// <summary>Context object shared with all benchmark commands during execution.</summary>
        private BenchmarkContext _context;

        /// <summary>Per-frame wall-clock time in milliseconds.</summary>
        private float[] _frameMs;

        /// <summary>Frame profiler instance for enabling/disabling profiling during benchmarks.</summary>
        private IFrameProfiler _frameProfiler;

        /// <summary>Per-frame GC generation-0 collection count.</summary>
        private int[] _gcGen0;

        /// <summary>Per-frame GC generation-1 collection count.</summary>
        private int[] _gcGen1;

        /// <summary>Per-frame GC generation-2 (full) collection count.</summary>
        private int[] _gcGen2;

        /// <summary>Per-frame worldgen jobs completed.</summary>
        private int[] _genCompleted;

        /// <summary>Per-frame slowest generation Complete() call in milliseconds.</summary>
        private float[] _genCompleteMaxMs;

        /// <summary>Per-frame count of generation Complete() calls that exceeded 1ms.</summary>
        private int[] _genCompleteStalls;

        /// <summary>Per-frame size of the Generated state set.</summary>
        private int[] _generatedSetSize;

        /// <summary>Per-frame worldgen jobs scheduled.</summary>
        private int[] _genScheduled;

        /// <summary>Per-frame bytes uploaded to GPU.</summary>
        private long[] _gpuUploadBytes;

        /// <summary>Per-frame GPU upload operation count.</summary>
        private int[] _gpuUploadCount;

        /// <summary>Per-frame MegaMeshBuffer grow (reallocation) events.</summary>
        private int[] _growEvents;

        /// <summary>UI label displaying keyboard shortcut hints at the bottom of the menu.</summary>
        private Label _hintLabel;

        /// <summary>Per-frame chunk mesh invalidation count.</summary>
        private int[] _invalidateCount;

        /// <summary>Per-frame LOD>0 mesh jobs completed.</summary>
        private int[] _lodCompleted;

        /// <summary>Per-frame LOD>0 mesh jobs scheduled.</summary>
        private int[] _lodScheduled;

        /// <summary>Whether the scenario picker menu is currently visible.</summary>
        private bool _menuOpen;

        /// <summary>Root visual element of the scenario picker menu panel.</summary>
        private VisualElement _menuPanel;

        /// <summary>Per-frame LOD0 mesh jobs completed.</summary>
        private int[] _meshCompleted;

        /// <summary>Per-frame slowest mesh Complete() call in milliseconds.</summary>
        private float[] _meshCompleteMaxMs;

        /// <summary>Per-frame count of mesh Complete() calls that exceeded 1ms.</summary>
        private int[] _meshCompleteStalls;

        /// <summary>Per-frame LOD0 mesh jobs scheduled.</summary>
        private int[] _meshScheduled;

        /// <summary>Metrics registry for reading per-frame snapshots during measurement.</summary>
        private MetricsRegistry _metrics;

        /// <summary>Pipeline stats instance for enabling/disabling stats during benchmarks.</summary>
        private IPipelineStats _pipelineStats;

        /// <summary>Player controller reference for physics body control during benchmarks.</summary>
        private PlayerController _playerController;

        /// <summary>UI labels for each scenario row in the picker menu.</summary>
        private Label[] _scenarioLabels;

        /// <summary>Per-frame time spent allocating NativeArrays for mesh output.</summary>
        private float[] _schedMeshAllocMs;

        /// <summary>Per-frame time spent filling the mesh candidate list.</summary>
        private float[] _schedMeshFillMs;

        /// <summary>Per-frame time spent filtering and sorting mesh candidates.</summary>
        private float[] _schedMeshFilterMs;

        /// <summary>Per-frame time spent flushing completed mesh data to GPU buffers.</summary>
        private float[] _schedMeshFlushMs;

        /// <summary>Per-frame time spent calling Schedule() on mesh jobs.</summary>
        private float[] _schedMeshScheduleMs;

        /// <summary>Per-frame per-section profiler timings (first dimension is section index).</summary>
        private float[][] _sectionMs;

        /// <summary>Currently highlighted index in the scenario picker menu.</summary>
        private int _selectedIndex;

        /// <summary>Debug settings asset containing default benchmark scenario and configuration.</summary>
        private DebugSettings _settings;

        /// <summary>UI label for the status toast message.</summary>
        private Label _statusLabel;

        /// <summary>Root visual element of the status toast panel.</summary>
        private VisualElement _statusPanel;

        /// <summary>Countdown timer for auto-hiding the status toast.</summary>
        private float _statusTimer;

        /// <summary>UI label for the menu title text.</summary>
        private Label _titleLabel;

        /// <summary>UIDocument component hosting the benchmark UI elements.</summary>
        private UIDocument _uiDocument;

        /// <summary>Root visual element of the UIDocument for all benchmark UI.</summary>
        private VisualElement _uiRoot;

        /// <summary>Whether a benchmark scenario is currently executing.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>The formatted summary text from the most recently completed benchmark run.</summary>
        public string LastSummary { get; private set; }

        /// <summary>Countdown timer for how long the summary remains displayed.</summary>
        public float SummaryDisplayTimer { get; private set; }

        /// <summary>Gets the currently selected benchmark scenario in the picker menu, or null if none available.</summary>
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

        /// <summary>Handles per-frame input for menu navigation and status toast countdown.</summary>
        private void Update()
        {
            // Countdown summary display timer
            if (SummaryDisplayTimer > 0f)
            {
                SummaryDisplayTimer -= Time.unscaledDeltaTime;
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

            if (IsRunning)
            {
                return;
            }

            // F5 to open menu
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                if (_context is
                    {
                        GameLoopPoco:
                        {
                            SpawnReady: true,
                        },
                    })
                {
                    OpenMenu();
                }
            }
        }

        /// <summary>
        ///     Safety net: if the MonoBehaviour is disabled mid-benchmark (e.g. scene change),
        ///     ensure ExternallyControlled is cleared so the player isn't permanently frozen.
        /// </summary>
        private void OnDisable()
        {
            if (IsRunning)
            {
                IsRunning = false;
                _activeCoroutine = null;

                PlayerPhysicsBody physicsBody = _playerController != null
                    ? _playerController.PhysicsBody : null;

                if (physicsBody != null)
                {
                    physicsBody.ExternallyControlled = false;
                }
            }
        }

        /// <summary>Initializes the benchmark runner with all required dependencies and builds the UI.</summary>
        public void Initialize(
            BenchmarkContext context,
            DebugSettings settings,
            MetricsRegistry metrics,
            PlayerController playerController,
            PanelSettings panelSettings,
            IFrameProfiler frameProfiler,
            IPipelineStats pipelineStats)
        {
            _context = context;
            _settings = settings;
            _metrics = metrics;
            _playerController = playerController;
            _frameProfiler = frameProfiler;
            _pipelineStats = pipelineStats;

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
                _context.Logger?.LogInfo("[Benchmark] Loaded " + _allScenarios.Length +
                                       " scenarios. Press F5 to open picker.");
            }
        }

        /// <summary>
        ///     Sets the game loop reference on the BenchmarkContext after late initialization.
        ///     Called by SessionBridgeSubsystem after creating the GameLoopPoco.
        /// </summary>
        public void SetGameLoopPoco(GameLoopPoco gameLoopPoco)
        {
            if (_context != null)
            {
                _context.GameLoopPoco = gameLoopPoco;
            }
        }

        /// <summary>Creates the UIDocument and builds the menu and status panel visual elements.</summary>
        private void BuildUI(PanelSettings panelSettings)
        {
            GameObject uiGo = new("BenchmarkUI");
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

        /// <summary>Builds the centered scenario picker menu panel with title, rows, and hint labels.</summary>
        private void BuildMenuPanel()
        {
            // Centered container
            VisualElement centerContainer = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                },
            };
            _uiRoot.Add(centerContainer);

            // Menu panel
            _menuPanel = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.92f),
                    paddingLeft = 16,
                    paddingRight = 16,
                    paddingTop = 12,
                    paddingBottom = 12,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    minWidth = 340,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.3f, 0.5f, 0.8f, 0.6f),
                    borderRightColor = new Color(0.3f, 0.5f, 0.8f, 0.6f),
                    borderTopColor = new Color(0.3f, 0.5f, 0.8f, 0.6f),
                    borderBottomColor = new Color(0.3f, 0.5f, 0.8f, 0.6f),
                },
            };
            centerContainer.Add(_menuPanel);

            // Title
            _titleLabel = CreateLabel("BENCHMARKS", 15, s_titleColor);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 8;
            _menuPanel.Add(_titleLabel);

            // Separator
            VisualElement sep = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    height = 1, backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.4f), marginBottom = 6,
                },
            };
            _menuPanel.Add(sep);

            // Scenario rows
            if (_allScenarios is
                {
                    Length: > 0,
                })
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
            VisualElement sep2 = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    height = 1, backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.4f), marginTop = 6, marginBottom = 4,
                },
            };
            _menuPanel.Add(sep2);

            // Hint
            _hintLabel = CreateLabel("\u2191\u2193 Select   Enter Run   Esc Close", 12, s_hintColor);
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _menuPanel.Add(_hintLabel);
        }

        /// <summary>Builds the top-center status toast panel for displaying progress messages.</summary>
        private void BuildStatusPanel()
        {
            // Status toast — top-center
            VisualElement topContainer = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 16,
                    alignItems = Align.Center,
                },
            };
            _uiRoot.Add(topContainer);

            _statusPanel = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.85f),
                    paddingLeft = 16,
                    paddingRight = 16,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                },
            };
            topContainer.Add(_statusPanel);

            _statusLabel = CreateLabel("", 14, s_runningColor);
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusPanel.Add(_statusLabel);
        }

        /// <summary>Displays a timed status toast message with the specified text and color.</summary>
        private void ShowStatus(string message, Color color)
        {
            _statusLabel.text = message;
            _statusLabel.style.color = color;
            _statusPanel.style.display = DisplayStyle.Flex;
            _statusTimer = StatusDuration;
        }

        /// <summary>Opens the scenario picker menu and refreshes the highlight state.</summary>
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

        /// <summary>Hides the scenario picker menu.</summary>
        private void CloseMenu()
        {
            _menuOpen = false;
            _menuPanel.style.display = DisplayStyle.None;
        }

        /// <summary>Updates the visual highlight on scenario labels to reflect the current selection.</summary>
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

        /// <summary>Creates a UI Toolkit label with the specified text, font size, and color.</summary>
        private static Label CreateLabel(string text, int fontSize, Color color)
        {
            Label label = new(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = fontSize,
                    color = color,
                    unityTextAlign = TextAnchor.UpperLeft,
                    marginTop = 0,
                    marginBottom = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                },
            };
            return label;
        }

        /// <summary>Allocates all per-frame parallel recording arrays at the specified capacity.</summary>
        private void AllocateArrays(int capacity)
        {
            _frameMs = new float[capacity];
            _sectionMs = new float[FrameProfilerSections.SectionCount][];

            for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
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

        /// <summary>Processes keyboard input for the scenario picker menu (navigation, selection, close).</summary>
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

        /// <summary>Starts executing the specified benchmark scenario as a coroutine.</summary>
        public void StartScenario(BenchmarkScenario scenario)
        {
            if (IsRunning)
            {
                _context.Logger?.LogWarning("[Benchmark] Already running.");
                return;
            }

            IsRunning = true;
            LastSummary = null;
            _activeCoroutine = StartCoroutine(RunScenarioCoroutine(scenario));
        }

        /// <summary>Main benchmark coroutine that executes phases, records metrics, and produces results.</summary>
        private IEnumerator RunScenarioCoroutine(BenchmarkScenario scenario)
        {
            _context.Logger?.LogInfo("[Benchmark] Starting scenario: " + scenario.ScenarioName);

            // Enable profiling
            _frameProfiler.Enabled = true;
            _pipelineStats.Enabled = true;

            // Disable tick-driven physics so benchmark commands can move the player directly.
            PlayerPhysicsBody physicsBody = _playerController != null
                ? _playerController.PhysicsBody : null;

            if (physicsBody != null)
            {
                physicsBody.ExternallyControlled = true;
            }

            int totalRecordedFrames = 0;
            BenchmarkPhase[] phases = scenario.Phases;

            if (phases == null || phases.Length == 0)
            {
                _context.Logger?.LogWarning("[Benchmark] Scenario has no phases.");
                FinishRun(null);
                yield break;
            }

            for (int p = 0; p < phases.Length; p++)
            {
                BenchmarkPhase phase = phases[p];
                _context.Logger?.LogInfo("[Benchmark] Phase " + (p + 1) + "/" + phases.Length +
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

                    for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
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
                float3 finalPos = new(
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
            BenchmarkCsvWriter.Write(result, outputDir, timestamp, _context.Logger);

            // Build and display summary
            string summary = BuildSummary(result);
            LastSummary = summary;
            SummaryDisplayTimer = SummaryDisplayDuration;
            _context.Logger?.LogInfo(summary);

            // Show completion status
            string passText = result.Passed ? "PASS" : "FAIL";
            Color passColor = result.Passed ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            ShowStatus(scenario.ScenarioName + " — " + passText +
                       "  (avg " + result.AvgFrameMs.ToString("F1") + "ms, " +
                       result.AvgFps.ToString("F0") + " FPS)", passColor);
            _statusTimer = 8f;

            IsRunning = false;
            _activeCoroutine = null;
        }

        /// <summary>Grows all per-frame recording arrays to the specified new capacity, preserving existing data.</summary>
        private void GrowArrays(int newCapacity)
        {
            float[] newFrameMs = new float[newCapacity];
            Array.Copy(_frameMs, newFrameMs, _capacity);
            _frameMs = newFrameMs;

            for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
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

        /// <summary>Computes timing statistics, pipeline totals, bottleneck detection, and pass/fail from recorded data.</summary>
        private BenchmarkResult BuildResult(BenchmarkScenario scenario, int totalFrames)
        {
            BenchmarkResult result = new()
            {
                ScenarioName = scenario.ScenarioName,
                TotalFrames = totalFrames,
                // Assign array references
                FrameMs = _frameMs,
                SectionMs = _sectionMs,
                GenCompleted = _genCompleted,
                MeshCompleted = _meshCompleted,
                LodCompleted = _lodCompleted,
                GpuUploadBytes = _gpuUploadBytes,
                GpuUploadCount = _gpuUploadCount,
                GrowEvents = _growEvents,
                GcGen0 = _gcGen0,
                GcGen1 = _gcGen1,
                GcGen2 = _gcGen2,
                GenScheduled = _genScheduled,
                MeshScheduled = _meshScheduled,
                LodScheduled = _lodScheduled,
                InvalidateCount = _invalidateCount,
                MeshCompleteMaxMs = _meshCompleteMaxMs,
                MeshCompleteStalls = _meshCompleteStalls,
                GenCompleteMaxMs = _genCompleteMaxMs,
                GenCompleteStalls = _genCompleteStalls,
                SchedMeshFillMs = _schedMeshFillMs,
                SchedMeshFilterMs = _schedMeshFilterMs,
                SchedMeshAllocMs = _schedMeshAllocMs,
                SchedMeshScheduleMs = _schedMeshScheduleMs,
                SchedMeshFlushMs = _schedMeshFlushMs,
                GeneratedSetSize = _generatedSetSize,
            };

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
            float[] sectionAvg = new float[FrameProfilerSections.SectionCount];

            for (int s = 0; s < FrameProfilerSections.SectionCount; s++)
            {
                float sum = 0f;

                for (int f = 0; f < totalFrames; f++)
                {
                    sum += _sectionMs[s][f];
                }

                sectionAvg[s] = sum / totalFrames;
            }

            // Sort section indices by avg ms descending
            int[] sectionOrder = new int[FrameProfilerSections.SectionCount];

            for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
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

                if (s == FrameProfilerSections.UpdateTotal || s == FrameProfilerSections.Frame)
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

                if (s == FrameProfilerSections.UpdateTotal || s == FrameProfilerSections.Frame)
                {
                    continue;
                }

                float pct = sectionAvg[s] / budgetMs * 100f;

                if (pct >= 15f)
                {
                    result.BottleneckDescription = FrameProfilerSections.SectionNames[s] + " (" +
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

        /// <summary>Formats a human-readable benchmark summary string with FPS, timing, pipeline, and bottleneck data.</summary>
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

                float pct = avg / result.AvgFrameMs * 100f;
                _summaryBuilder.Append(FrameProfilerSections.SectionNames[s]);
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

        /// <summary>Cleans up after a benchmark run, restoring physics control to the player.</summary>
        private void FinishRun(BenchmarkResult result)
        {
            IsRunning = false;
            _activeCoroutine = null;

            PlayerPhysicsBody physicsBody = _playerController != null
                ? _playerController.PhysicsBody : null;

            if (physicsBody != null)
            {
                physicsBody.ExternallyControlled = false;
            }
        }
    }
}
