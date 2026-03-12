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

        // Pre-allocated for summary generation
        private readonly StringBuilder _summaryBuilder = new StringBuilder(2048);

        // Results display
        private string _lastSummary;
        private float _summaryDisplayTimer;
        private const float SummaryDisplayDuration = 15f;

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

            // Capture current forward direction for the fly path
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                _flyDirection = mainCamera.transform.forward;
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

            // Record frame data into pre-allocated parallel arrays
            int f = _frameCount;

            if (f < _capacity)
            {
                _frameMs[f] = dt * 1000f;

                for (int i = 0; i < FrameProfiler.SectionCount; i++)
                {
                    _sectionMs[i][f] = FrameProfiler.GetMs(i);
                }

                _genScheduled[f] = PipelineStats.GenScheduled;
                _genCompleted[f] = PipelineStats.GenCompleted;
                _meshScheduled[f] = PipelineStats.MeshScheduled;
                _meshCompleted[f] = PipelineStats.MeshCompleted;
                _lodScheduled[f] = PipelineStats.LODScheduled;
                _lodCompleted[f] = PipelineStats.LODCompleted;
                _gpuUploadBytes[f] = PipelineStats.GpuUploadBytes;
                _gpuUploadCount[f] = PipelineStats.GpuUploadCount;
                _growEvents[f] = PipelineStats.GrowEvents;

                _frameCount++;
            }

            // Check if benchmark is complete
            if (_elapsed >= _duration)
            {
                FinishBenchmark();
            }
        }

        private void FinishBenchmark()
        {
            _running = false;

            // Re-enable PlayerController
            if (_playerController != null)
            {
                _playerController.enabled = true;
            }

            WriteCsv();
            string summary = BuildSummary();
            _lastSummary = summary;
            _summaryDisplayTimer = SummaryDisplayDuration;

            UnityEngine.Debug.Log(summary);
        }

        private void WriteCsv()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
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

            float gpuMB = totalGpuBytes / (1024f * 1024f);
            _summaryBuilder.Append("GPU Upload: ");
            _summaryBuilder.Append(gpuMB.ToString("F1"));
            _summaryBuilder.Append(" MB total  (");
            _summaryBuilder.Append((gpuMB / durationSec).ToString("F1"));
            _summaryBuilder.AppendLine(" MB/s)");

            _summaryBuilder.Append("Grow events: ");
            _summaryBuilder.AppendLine(totalGrow.ToString());

            _summaryBuilder.AppendLine("--- Bottleneck ---");
            _summaryBuilder.AppendLine(bottleneck);
            _summaryBuilder.Append("=========================");

            return _summaryBuilder.ToString();
        }
    }
}
