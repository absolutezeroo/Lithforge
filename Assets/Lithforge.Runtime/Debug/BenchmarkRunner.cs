using System;
using System.Globalization;
using System.IO;
using System.Text;
using Lithforge.Runtime.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Automated benchmark that flies the player forward for a configurable duration,
    /// recording per-frame profiling data. Activated by F5. Writes a CSV report to
    /// Application.persistentDataPath and logs a summary to the console.
    /// Uses parallel arrays (not per-frame structs) to avoid heap allocation during recording.
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        private PlayerController _playerController;
        private Transform _playerTransform;
        private GameLoop _gameLoop;
        private float _flySpeed = 50f;
        private float _duration = 10f;

        private bool _running;
        private float _elapsed;
        private Vector3 _flyDirection;
        private Camera _benchmarkCamera;
        private int _frameCount;
        private int _capacity;

        // Parallel arrays for per-frame recording — pre-allocated in Initialize, no per-frame alloc
        private float[] _frameMs;
        private float[][] _sectionMs; // [sectionIndex][frameIndex]
        private int[] _genScheduled;
        private int[] _genCompleted;
        private int[] _meshScheduled;
        private int[] _meshCompleted;
        private int[] _lodScheduled;
        private int[] _lodCompleted;
        private long[] _gpuUploadBytes;
        private int[] _gpuUploadCount;
        private int[] _growEvents;
        private int[] _gcGen0;
        private int[] _gcGen1;
        private int[] _gcGen2;
        private float[] _meshCompleteMaxMs;
        private int[] _meshCompleteStalls;
        private float[] _genCompleteMaxMs;
        private int[] _genCompleteStalls;
        private float[] _pollMeshDisposalsMs;
        private float[] _pollMeshRelightMs;
        private float[] _pollMeshUploadMs;
        private float[] _pollMeshIterateMs;
        private float[] _pollMeshFirstIsCompletedMs;

        // Previous-frame snapshot of PipelineStats (to align with FrameProfiler which reports N-1)
        private int _prevGenScheduled;
        private int _prevGenCompleted;
        private int _prevMeshScheduled;
        private int _prevMeshCompleted;
        private int _prevLodScheduled;
        private int _prevLodCompleted;
        private long _prevGpuUploadBytes;
        private int _prevGpuUploadCount;
        private int _prevGrowEvents;
        private int _prevGcGen0;
        private int _prevGcGen1;
        private int _prevGcGen2;
        private float _prevMeshCompleteMaxMs;
        private int _prevMeshCompleteStalls;
        private float _prevGenCompleteMaxMs;
        private int _prevGenCompleteStalls;
        private float _prevPollMeshDisposalsMs;
        private float _prevPollMeshRelightMs;
        private float _prevPollMeshUploadMs;
        private float _prevPollMeshIterateMs;
        private float _prevPollMeshFirstIsCompletedMs;

        // Pre-allocated for summary generation
        private readonly StringBuilder _summaryBuilder = new StringBuilder(2048);

        // Results display
        private string _lastSummary;
        private float _summaryDisplayTimer;
        private const float _summaryDisplayDuration = 15f;

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

        public void Initialize(
            PlayerController playerController,
            Transform playerTransform,
            GameLoop gameLoop,
            float flySpeed,
            float duration)
        {
            _playerController = playerController;
            _playerTransform = playerTransform;
            _gameLoop = gameLoop;
            _flySpeed = flySpeed;
            _duration = duration;

            // Pre-allocate for estimated max frames (duration * 200fps headroom)
            _capacity = Mathf.Max(2000, (int)(duration * 200f));
            _frameMs = new float[_capacity];
            _sectionMs = new float[FrameProfiler.SectionCount][];

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                _sectionMs[i] = new float[_capacity];
            }

            _genScheduled = new int[_capacity];
            _genCompleted = new int[_capacity];
            _meshScheduled = new int[_capacity];
            _meshCompleted = new int[_capacity];
            _lodScheduled = new int[_capacity];
            _lodCompleted = new int[_capacity];
            _gpuUploadBytes = new long[_capacity];
            _gpuUploadCount = new int[_capacity];
            _growEvents = new int[_capacity];
            _gcGen0 = new int[_capacity];
            _gcGen1 = new int[_capacity];
            _gcGen2 = new int[_capacity];
            _meshCompleteMaxMs = new float[_capacity];
            _meshCompleteStalls = new int[_capacity];
            _genCompleteMaxMs = new float[_capacity];
            _genCompleteStalls = new int[_capacity];
            _pollMeshDisposalsMs = new float[_capacity];
            _pollMeshRelightMs = new float[_capacity];
            _pollMeshUploadMs = new float[_capacity];
            _pollMeshIterateMs = new float[_capacity];
            _pollMeshFirstIsCompletedMs = new float[_capacity];
        }

        private void Update()
        {
            // Countdown summary display timer
            if (_summaryDisplayTimer > 0f)
            {
                _summaryDisplayTimer -= Time.unscaledDeltaTime;
            }

            if (_running)
            {
                UpdateBenchmark();
                return;
            }

            // F5 to start benchmark
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                if (_gameLoop != null && _gameLoop.SpawnReady)
                {
                    StartBenchmark();
                }
            }
        }

        private void StartBenchmark()
        {
            _frameCount = 0;
            _elapsed = 0f;

            // Enable profiling systems
            FrameProfiler.Enabled = true;
            PipelineStats.Enabled = true;

            // Disable PlayerController to prevent double-movement
            if (_playerController != null)
            {
                _playerController.SetFlyMode(true, true, _flySpeed);
                _playerController.enabled = false;
            }

            // Use camera's horizontal forward direction (Y=0) for reproducible benchmark paths.
            // This prevents vertical drift from camera pitch affecting chunk load patterns.
            _benchmarkCamera = Camera.main;

            if (_benchmarkCamera != null)
            {
                Vector3 fwd = _benchmarkCamera.transform.forward;
                fwd.y = 0f;

                if (fwd.sqrMagnitude < 0.001f)
                {
                    // Camera looking straight up/down — fallback to transform forward projected
                    fwd = _benchmarkCamera.transform.up.y > 0 ? Vector3.forward : Vector3.back;
                }

                _flyDirection = fwd.normalized;
            }
            else
            {
                _flyDirection = Vector3.forward;
            }

            _running = true;
            _lastSummary = null;

            UnityEngine.Debug.Log("[Benchmark] Started");
        }

        private void UpdateBenchmark()
        {
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;

            // Move player forward
            if (_playerTransform != null)
            {
                _playerTransform.position += _flyDirection * (_flySpeed * dt);
            }

            // Lock camera orientation to prevent pitch drift during benchmark
            if (_benchmarkCamera != null)
            {
                _benchmarkCamera.transform.forward = _flyDirection;
            }

            // Record frame data into pre-allocated parallel arrays
            int f = _frameCount;

            if (f < _capacity)
            {
                _frameMs[f] = dt * 1000f;

                // FrameProfiler sections: already frame N-1 (read via GetMs which was set in BeginFrame)
                for (int i = 0; i < FrameProfiler.SectionCount; i++)
                {
                    _sectionMs[i][f] = FrameProfiler.GetMs(i);
                }

                // PipelineStats: use PREVIOUS frame's snapshot (to align with FrameProfiler N-1)
                _genScheduled[f] = _prevGenScheduled;
                _genCompleted[f] = _prevGenCompleted;
                _meshScheduled[f] = _prevMeshScheduled;
                _meshCompleted[f] = _prevMeshCompleted;
                _lodScheduled[f] = _prevLodScheduled;
                _lodCompleted[f] = _prevLodCompleted;
                _gpuUploadBytes[f] = _prevGpuUploadBytes;
                _gpuUploadCount[f] = _prevGpuUploadCount;
                _growEvents[f] = _prevGrowEvents;
                _gcGen0[f] = _prevGcGen0;
                _gcGen1[f] = _prevGcGen1;
                _gcGen2[f] = _prevGcGen2;
                _meshCompleteMaxMs[f] = _prevMeshCompleteMaxMs;
                _meshCompleteStalls[f] = _prevMeshCompleteStalls;
                _genCompleteMaxMs[f] = _prevGenCompleteMaxMs;
                _genCompleteStalls[f] = _prevGenCompleteStalls;
                _pollMeshDisposalsMs[f] = _prevPollMeshDisposalsMs;
                _pollMeshRelightMs[f] = _prevPollMeshRelightMs;
                _pollMeshUploadMs[f] = _prevPollMeshUploadMs;
                _pollMeshIterateMs[f] = _prevPollMeshIterateMs;
                _pollMeshFirstIsCompletedMs[f] = _prevPollMeshFirstIsCompletedMs;

                _frameCount++;
            }

            // Snapshot CURRENT frame's PipelineStats for next frame's recording
            _prevGenScheduled = PipelineStats.GenScheduled;
            _prevGenCompleted = PipelineStats.GenCompleted;
            _prevMeshScheduled = PipelineStats.MeshScheduled;
            _prevMeshCompleted = PipelineStats.MeshCompleted;
            _prevLodScheduled = PipelineStats.LODScheduled;
            _prevLodCompleted = PipelineStats.LODCompleted;
            _prevGpuUploadBytes = PipelineStats.GpuUploadBytes;
            _prevGpuUploadCount = PipelineStats.GpuUploadCount;
            _prevGrowEvents = PipelineStats.GrowEvents;
            _prevGcGen0 = PipelineStats.GcGen0;
            _prevGcGen1 = PipelineStats.GcGen1;
            _prevGcGen2 = PipelineStats.GcGen2;
            _prevMeshCompleteMaxMs = PipelineStats.MeshCompleteMaxMs;
            _prevMeshCompleteStalls = PipelineStats.MeshCompleteStalls;
            _prevGenCompleteMaxMs = PipelineStats.GenCompleteMaxMs;
            _prevGenCompleteStalls = PipelineStats.GenCompleteStalls;
            _prevPollMeshDisposalsMs = PipelineStats.PollMeshDisposalsMs;
            _prevPollMeshRelightMs = PipelineStats.PollMeshRelightMs;
            _prevPollMeshUploadMs = PipelineStats.PollMeshUploadMs;
            _prevPollMeshIterateMs = PipelineStats.PollMeshIterateMs;
            _prevPollMeshFirstIsCompletedMs = PipelineStats.PollMeshFirstIsCompletedMs;

            // Check if benchmark is complete
            if (_elapsed >= _duration)
            {
                FinishBenchmark();
            }
        }

        private void FinishBenchmark()
        {
            _running = false;
            _benchmarkCamera = null;

            // Re-enable PlayerController
            if (_playerController != null)
            {
                _playerController.enabled = true;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            WriteCsv(timestamp);
            WritePng(timestamp);
            string summary = BuildSummary();
            _lastSummary = summary;
            _summaryDisplayTimer = _summaryDisplayDuration;

            UnityEngine.Debug.Log(summary);
        }

        private void WriteCsv(string timestamp)
        {
            string path = Path.Combine(Application.persistentDataPath,
                "benchmark_" + timestamp + ".csv");

            StringBuilder csv = new StringBuilder(1024 * 64);

            // Header
            csv.Append("frame_index,frame_ms");

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                csv.Append(',');
                csv.Append(FrameProfiler.SectionNames[i]);
                csv.Append("_ms");
            }

            csv.Append(",gen_scheduled,gen_completed,mesh_scheduled,mesh_completed");
            csv.Append(",lod_scheduled,lod_completed,gpu_upload_bytes,gpu_upload_count,grow_events");
            csv.Append(",gc_gen0,gc_gen1,gc_gen2,mesh_complete_max_ms,mesh_complete_stalls,gen_complete_max_ms,gen_complete_stalls");
            csv.Append(",pm_disposals_ms,pm_relight_ms,pm_upload_ms,pm_iterate_ms,pm_first_iscompleted_ms");
            csv.AppendLine();

            // Data rows
            for (int f = 0; f < _frameCount; f++)
            {
                csv.Append(f);
                csv.Append(',');
                csv.Append(_frameMs[f].ToString("F3", CultureInfo.InvariantCulture));

                for (int i = 0; i < FrameProfiler.SectionCount; i++)
                {
                    csv.Append(',');
                    csv.Append(_sectionMs[i][f].ToString("F3", CultureInfo.InvariantCulture));
                }

                csv.Append(',');
                csv.Append(_genScheduled[f]);
                csv.Append(',');
                csv.Append(_genCompleted[f]);
                csv.Append(',');
                csv.Append(_meshScheduled[f]);
                csv.Append(',');
                csv.Append(_meshCompleted[f]);
                csv.Append(',');
                csv.Append(_lodScheduled[f]);
                csv.Append(',');
                csv.Append(_lodCompleted[f]);
                csv.Append(',');
                csv.Append(_gpuUploadBytes[f]);
                csv.Append(',');
                csv.Append(_gpuUploadCount[f]);
                csv.Append(',');
                csv.Append(_growEvents[f]);
                csv.Append(',');
                csv.Append(_gcGen0[f]);
                csv.Append(',');
                csv.Append(_gcGen1[f]);
                csv.Append(',');
                csv.Append(_gcGen2[f]);
                csv.Append(',');
                csv.Append(_meshCompleteMaxMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_meshCompleteStalls[f]);
                csv.Append(',');
                csv.Append(_genCompleteMaxMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_genCompleteStalls[f]);
                csv.Append(',');
                csv.Append(_pollMeshDisposalsMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_pollMeshRelightMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_pollMeshUploadMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_pollMeshIterateMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(_pollMeshFirstIsCompletedMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            File.WriteAllText(path, csv.ToString());
            UnityEngine.Debug.Log("[Benchmark] CSV written to: " + path);
        }

        private string BuildSummary()
        {
            int count = _frameCount;

            if (count == 0)
            {
                return "[Benchmark] No frames recorded.";
            }

            // Compute FPS and frame time stats
            float totalFrameMs = 0f;
            float minFrameMs = float.MaxValue;
            float maxFrameMs = 0f;

            for (int i = 0; i < count; i++)
            {
                float ms = _frameMs[i];
                totalFrameMs += ms;

                if (ms < minFrameMs)
                {
                    minFrameMs = ms;
                }

                if (ms > maxFrameMs)
                {
                    maxFrameMs = ms;
                }
            }

            float avgFrameMs = Mathf.Max(totalFrameMs / count, 0.001f);
            float maxFrameMsClamped = Mathf.Max(maxFrameMs, 0.001f);
            float minFrameMsClamped = Mathf.Max(minFrameMs, 0.001f);
            float avgFps = 1000f / avgFrameMs;
            float minFps = 1000f / maxFrameMsClamped;
            float maxFps = 1000f / minFrameMsClamped;

            // Percentiles (simple sorted approach)
            float[] sortedMs = new float[count];

            for (int i = 0; i < count; i++)
            {
                sortedMs[i] = _frameMs[i];
            }

            Array.Sort(sortedMs);

            float p1Ms = Mathf.Max(sortedMs[(int)(count * 0.01f)], 0.001f);
            float p99Ms = Mathf.Max(sortedMs[Math.Min((int)(count * 0.99f), count - 1)], 0.001f);
            float p1Fps = 1000f / p99Ms;
            float p99Fps = 1000f / p1Ms;

            // Section averages
            float[] sectionAvg = new float[FrameProfiler.SectionCount];

            for (int s = 0; s < FrameProfiler.SectionCount; s++)
            {
                float sum = 0f;

                for (int f = 0; f < count; f++)
                {
                    sum += _sectionMs[s][f];
                }

                sectionAvg[s] = sum / count;
            }

            // Pipeline totals
            int totalGen = 0;
            int totalMesh = 0;
            long totalGpuBytes = 0;
            int totalGrow = 0;

            for (int f = 0; f < count; f++)
            {
                totalGen += _genCompleted[f];
                totalMesh += _meshCompleted[f];
                totalGpuBytes += _gpuUploadBytes[f];
                totalGrow += _growEvents[f];
            }

            float durationSec = Mathf.Max(totalFrameMs / 1000f, 0.001f);

            // Find top costs (sort section indices by avg ms descending)
            int[] sectionOrder = new int[FrameProfiler.SectionCount];

            for (int i = 0; i < FrameProfiler.SectionCount; i++)
            {
                sectionOrder[i] = i;
            }

            // Simple insertion sort for 14 elements
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

            // Bottleneck detection: highest avg that exceeds 15% of frame budget (16.67ms)
            float budgetMs = 16.667f;
            string bottleneck = "None detected";

            for (int i = 0; i < sectionOrder.Length; i++)
            {
                int s = sectionOrder[i];

                // Skip aggregate sections
                if (s == FrameProfiler.UpdateTotal || s == FrameProfiler.Frame)
                {
                    continue;
                }

                float pct = (sectionAvg[s] / budgetMs) * 100f;

                if (pct >= 15f)
                {
                    bottleneck = FrameProfiler.SectionNames[s] + " (" +
                        sectionAvg[s].ToString("F1") + "ms, " +
                        pct.ToString("F1") + "% of frame)";
                    break;
                }
            }

            // Build summary
            _summaryBuilder.Clear();
            _summaryBuilder.AppendLine("=== BENCHMARK RESULTS ===");
            _summaryBuilder.Append("Duration:  ");
            _summaryBuilder.Append(durationSec.ToString("F1"));
            _summaryBuilder.Append("s  |  Frames: ");
            _summaryBuilder.AppendLine(count.ToString());

            _summaryBuilder.Append("FPS:       avg ");
            _summaryBuilder.Append(avgFps.ToString("F1"));
            _summaryBuilder.Append("  min ");
            _summaryBuilder.Append(minFps.ToString("F0"));
            _summaryBuilder.Append("  max ");
            _summaryBuilder.Append(maxFps.ToString("F0"));
            _summaryBuilder.Append("  p1 ");
            _summaryBuilder.Append(p1Fps.ToString("F0"));
            _summaryBuilder.Append("  p99 ");
            _summaryBuilder.AppendLine(p99Fps.ToString("F0"));

            _summaryBuilder.Append("Frame:     avg ");
            _summaryBuilder.Append(avgFrameMs.ToString("F1"));
            _summaryBuilder.Append("ms  max ");
            _summaryBuilder.Append(maxFrameMs.ToString("F1"));
            _summaryBuilder.Append("ms  p99 ");
            _summaryBuilder.Append(p99Ms.ToString("F1"));
            _summaryBuilder.AppendLine("ms");

            _summaryBuilder.AppendLine("--- Top costs (avg ms) ---");

            // Show top 4 non-aggregate sections
            int shown = 0;

            for (int i = 0; i < sectionOrder.Length && shown < 4; i++)
            {
                int s = sectionOrder[i];

                if (s == FrameProfiler.UpdateTotal || s == FrameProfiler.Frame)
                {
                    continue;
                }

                float pct = (sectionAvg[s] / avgFrameMs) * 100f;
                _summaryBuilder.Append(FrameProfiler.SectionNames[s]);
                _summaryBuilder.Append(":  ");
                _summaryBuilder.Append(sectionAvg[s].ToString("F1"));
                _summaryBuilder.Append("ms (");
                _summaryBuilder.Append(pct.ToString("F1"));
                _summaryBuilder.AppendLine("%)");
                shown++;
            }

            _summaryBuilder.AppendLine("--- Pipeline ---");
            _summaryBuilder.Append("Generated:  ");
            _summaryBuilder.Append(totalGen);
            _summaryBuilder.Append(" chunks  (");
            _summaryBuilder.Append((totalGen / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine("/s)");

            _summaryBuilder.Append("Meshed:     ");
            _summaryBuilder.Append(totalMesh);
            _summaryBuilder.Append(" chunks  (");
            _summaryBuilder.Append((totalMesh / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine("/s)");

            float gpuMb = totalGpuBytes / (1024f * 1024f);
            _summaryBuilder.Append("GPU Upload: ");
            _summaryBuilder.Append(gpuMb.ToString("F1"));
            _summaryBuilder.Append(" MB total  (");
            _summaryBuilder.Append((gpuMb / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine(" MB/s)");

            _summaryBuilder.Append("Grow events: ");
            _summaryBuilder.AppendLine(totalGrow.ToString());

            _summaryBuilder.AppendLine("--- Bottleneck ---");
            _summaryBuilder.AppendLine(bottleneck);
            _summaryBuilder.Append("=========================");

            return _summaryBuilder.ToString();
        }

        // 3x5 bitmap font for PNG axis labels. Glyphs: 0-9 (indices 0-9), '.' (index 10).
        // Each glyph is 5 rows (top to bottom). Each row: bit 2=left, bit 1=center, bit 0=right.
        private static readonly byte[][] _fontGlyphs = new byte[][]
        {
            new byte[] { 7, 5, 5, 5, 7 }, // 0
            new byte[] { 2, 6, 2, 2, 7 }, // 1
            new byte[] { 7, 1, 7, 4, 7 }, // 2
            new byte[] { 7, 1, 7, 1, 7 }, // 3
            new byte[] { 5, 5, 7, 1, 1 }, // 4
            new byte[] { 7, 4, 7, 1, 7 }, // 5
            new byte[] { 7, 4, 7, 5, 7 }, // 6
            new byte[] { 7, 1, 1, 1, 1 }, // 7
            new byte[] { 7, 5, 7, 5, 7 }, // 8
            new byte[] { 7, 5, 7, 1, 7 }, // 9
            new byte[] { 0, 0, 0, 0, 2 }, // .
        };

        private void WritePng(string timestamp)
        {
            int count = _frameCount;

            if (count < 2)
            {
                return;
            }

            // Layout
            const int marginLeft = 50;
            const int marginRight = 10;
            const int marginTop = 10;
            const int marginBottom = 10;
            const int maxGraphWidth = 1800;
            const int graphHeight = 400;

            int graphWidth;
            int framesPerBin;

            if (count <= maxGraphWidth)
            {
                graphWidth = count;
                framesPerBin = 1;
            }
            else
            {
                graphWidth = maxGraphWidth;
                framesPerBin = (count + maxGraphWidth - 1) / maxGraphWidth;
            }

            int imgWidth = graphWidth + marginLeft + marginRight;
            int imgHeight = graphHeight + marginTop + marginBottom;

            Color32[] pixels = new Color32[imgWidth * imgHeight];

            // Background
            Color32 bgColor = new Color32(25, 25, 30, 255);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }

            // Graph area background
            Color32 graphBgColor = new Color32(35, 35, 42, 255);

            for (int gy = 0; gy < graphHeight; gy++)
            {
                for (int gx = 0; gx < graphWidth; gx++)
                {
                    pixels[(marginBottom + gy) * imgWidth + (marginLeft + gx)] = graphBgColor;
                }
            }

            // Find max frame time
            float maxMs = 0f;

            for (int f = 0; f < count; f++)
            {
                if (_frameMs[f] > maxMs)
                {
                    maxMs = _frameMs[f];
                }
            }

            // Auto-scale Y axis
            float yMax;

            if (maxMs <= 20f)
            {
                yMax = 20f;
            }
            else if (maxMs <= 34f)
            {
                yMax = 34f;
            }
            else if (maxMs <= 50f)
            {
                yMax = 50f;
            }
            else if (maxMs <= 100f)
            {
                yMax = 100f;
            }
            else if (maxMs <= 200f)
            {
                yMax = 200f;
            }
            else
            {
                yMax = maxMs * 1.1f;
            }

            // Reference lines
            Color32 line60Color = new Color32(255, 255, 255, 100);
            Color32 line30Color = new Color32(220, 80, 80, 100);
            Color32 labelColor = new Color32(200, 200, 200, 255);

            DrawHorizontalLine(pixels, imgWidth, imgHeight,
                marginLeft, graphWidth, marginBottom, graphHeight, 16.667f, yMax, line60Color);

            int lineY60 = Mathf.RoundToInt((16.667f / yMax) * graphHeight);
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom + lineY60 - 5, "16.7", labelColor);

            int lineY30 = Mathf.RoundToInt((33.333f / yMax) * graphHeight);

            if (lineY30 < graphHeight)
            {
                DrawHorizontalLine(pixels, imgWidth, imgHeight,
                    marginLeft, graphWidth, marginBottom, graphHeight, 33.333f, yMax, line30Color);
                DrawTextOnPixels(pixels, imgWidth, imgHeight,
                    2, marginBottom + lineY30 - 5, "33.3", labelColor);
            }

            // Y-axis labels: 0 and max
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom, "0", labelColor);
            DrawTextOnPixels(pixels, imgWidth, imgHeight,
                2, marginBottom + graphHeight - 12, yMax.ToString("F0"), labelColor);

            // Frame time bars
            Color32 greenColor = new Color32(0, 200, 0, 255);
            Color32 yellowColor = new Color32(220, 200, 0, 255);
            Color32 redColor = new Color32(220, 40, 40, 255);

            for (int bin = 0; bin < graphWidth; bin++)
            {
                int startFrame = bin * framesPerBin;
                int endFrame = Mathf.Min(startFrame + framesPerBin, count);

                float ms = 0f;
                int binCount = 0;

                for (int f = startFrame; f < endFrame; f++)
                {
                    ms += _frameMs[f];
                    binCount++;
                }

                if (binCount > 0)
                {
                    ms /= binCount;
                }

                int barH = Mathf.Clamp(Mathf.RoundToInt((ms / yMax) * graphHeight), 1, graphHeight);

                Color32 barColor;

                if (ms < 16.667f)
                {
                    barColor = greenColor;
                }
                else if (ms < 33.333f)
                {
                    barColor = yellowColor;
                }
                else
                {
                    barColor = redColor;
                }

                int px = marginLeft + bin;

                for (int row = 0; row < barH; row++)
                {
                    int py = marginBottom + row;

                    if (py >= 0 && py < imgHeight && px >= 0 && px < imgWidth)
                    {
                        pixels[py * imgWidth + px] = barColor;
                    }
                }
            }

            // Encode to PNG
            Texture2D tex = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);

            string path = Path.Combine(Application.persistentDataPath,
                "benchmark_" + timestamp + ".png");
            File.WriteAllBytes(path, png);
            UnityEngine.Debug.Log("[Benchmark] PNG written to: " + path);
        }

        /// <summary>
        /// Draws a dashed horizontal reference line on the pixel buffer at the given ms value.
        /// </summary>
        private static void DrawHorizontalLine(
            Color32[] pixels, int imgW, int imgH,
            int graphX, int graphW, int graphY, int graphH,
            float ms, float yMax, Color32 color)
        {
            int y = graphY + Mathf.RoundToInt((ms / yMax) * graphH);

            if (y < 0 || y >= imgH)
            {
                return;
            }

            for (int x = graphX; x < graphX + graphW; x++)
            {
                // Dashed: 4px on, 4px off
                if (((x - graphX) / 4) % 2 == 0 && x >= 0 && x < imgW)
                {
                    pixels[y * imgW + x] = color;
                }
            }
        }

        /// <summary>
        /// Renders a string of digits and periods onto a pixel buffer using a 3x5 bitmap font at 2x scale.
        /// Position (x, y) is the bottom-left corner of the text in texture coordinates (Y=0 at bottom).
        /// </summary>
        private static void DrawTextOnPixels(
            Color32[] pixels, int imgW, int imgH,
            int x, int y, string text, Color32 color)
        {
            const int glyphW = 3;
            const int glyphH = 5;
            const int scale = 2;

            int cx = x;

            for (int c = 0; c < text.Length; c++)
            {
                char ch = text[c];
                int glyphIndex;

                if (ch >= '0' && ch <= '9')
                {
                    glyphIndex = ch - '0';
                }
                else if (ch == '.')
                {
                    glyphIndex = 10;
                }
                else
                {
                    // Unknown character treated as space
                    cx += (glyphW + 1) * scale;
                    continue;
                }

                byte[] glyph = _fontGlyphs[glyphIndex];

                for (int row = 0; row < glyphH; row++)
                {
                    byte rowBits = glyph[row];

                    for (int col = 0; col < glyphW; col++)
                    {
                        if ((rowBits & (1 << (glyphW - 1 - col))) != 0)
                        {
                            for (int sy = 0; sy < scale; sy++)
                            {
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = cx + col * scale + sx;
                                    int py = y + (glyphH - 1 - row) * scale + sy;

                                    if (px >= 0 && px < imgW && py >= 0 && py < imgH)
                                    {
                                        pixels[py * imgW + px] = color;
                                    }
                                }
                            }
                        }
                    }
                }

                cx += (glyphW + 1) * scale;
            }
        }
    }
}
