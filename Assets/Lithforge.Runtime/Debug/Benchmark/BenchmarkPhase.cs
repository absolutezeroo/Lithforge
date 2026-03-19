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
        /// <summary>Human-readable name for this phase (used in CSV output).</summary>
        [Tooltip("Human-readable name for this phase (used in CSV output)")]
        [SerializeField] private string phaseName = "Phase";

        /// <summary>Commands to execute in order within this phase.</summary>
        [Tooltip("Commands to execute in order within this phase")]
        [SerializeField] private BenchmarkCommand[] commands;

        /// <summary>Number of frames to skip before recording metrics (allows pipeline warmup).</summary>
        [Tooltip("Number of frames to skip before recording metrics (allows pipeline warmup)")]
        [SerializeField] private int warmupFrames = 60;

        /// <summary>Number of frames to record metrics for.</summary>
        [Tooltip("Number of frames to record metrics for")]
        [SerializeField] private int measurementFrames = 300;

        /// <summary>Gets the human-readable name for this phase.</summary>
        public string PhaseName
        {
            get { return phaseName; }
        }

        /// <summary>Gets the ordered array of commands to execute in this phase.</summary>
        public BenchmarkCommand[] Commands
        {
            get { return commands; }
        }

        /// <summary>Gets the number of warmup frames to skip before measurement begins.</summary>
        public int WarmupFrames
        {
            get { return warmupFrames; }
        }

        /// <summary>Gets the number of frames to record metrics for during measurement.</summary>
        public int MeasurementFrames
        {
            get { return measurementFrames; }
        }
    }
}
