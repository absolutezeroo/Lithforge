using System;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// A single phase within a benchmark scenario. Contains an ordered list of commands
    /// to execute sequentially. Phases run one after another within a scenario.
    /// </summary>
    [Serializable]
    public sealed class BenchmarkPhase
    {
        [Tooltip("Human-readable name for this phase (used in CSV output)")]
        [SerializeField] private string phaseName = "Phase";

        [Tooltip("Commands to execute in order within this phase")]
        [SerializeField] private BenchmarkCommand[] commands;

        [Tooltip("Number of frames to skip before recording metrics (allows pipeline warmup)")]
        [SerializeField] private int warmupFrames = 60;

        [Tooltip("Number of frames to record metrics for")]
        [SerializeField] private int measurementFrames = 300;

        public string PhaseName
        {
            get { return phaseName; }
        }

        public BenchmarkCommand[] Commands
        {
            get { return commands; }
        }

        public int WarmupFrames
        {
            get { return warmupFrames; }
        }

        public int MeasurementFrames
        {
            get { return measurementFrames; }
        }
    }
}
