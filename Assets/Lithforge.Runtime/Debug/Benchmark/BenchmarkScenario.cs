using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// ScriptableObject defining a complete benchmark scenario.
    /// Contains one or more phases, each with commands to execute and metrics to record.
    /// Configured in the editor and referenced by DebugSettings.DefaultBenchmarkScenario.
    /// </summary>
    [CreateAssetMenu(fileName = "BenchmarkScenario", menuName = "Lithforge/Benchmark/Scenario")]
    public sealed class BenchmarkScenario : ScriptableObject
    {
        /// <summary>Human-readable scenario name (used in CSV filename and summary).</summary>
        [Tooltip("Human-readable scenario name (used in CSV filename and summary)")]
        [SerializeField] private string scenarioName = "Default Benchmark";

        /// <summary>Ordered list of phases to execute sequentially.</summary>
        [Tooltip("Ordered list of phases to execute")]
        [SerializeField] private BenchmarkPhase[] phases;

        /// <summary>Maximum average frame time in milliseconds to consider the benchmark passed.</summary>
        [Tooltip("Maximum average frame time in ms to consider the benchmark passed")]
        [SerializeField] private float maxAvgFrameTimeMs = 16.67f;

        /// <summary>Gets the human-readable scenario name.</summary>
        public string ScenarioName
        {
            get { return scenarioName; }
        }

        /// <summary>Gets the ordered array of phases in this scenario.</summary>
        public BenchmarkPhase[] Phases
        {
            get { return phases; }
        }

        /// <summary>Gets the pass/fail threshold for average frame time in milliseconds.</summary>
        public float MaxAvgFrameTimeMs
        {
            get { return maxAvgFrameTimeMs; }
        }
    }
}
