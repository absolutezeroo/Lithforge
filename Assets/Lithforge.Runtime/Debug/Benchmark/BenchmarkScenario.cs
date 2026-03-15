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
        [Tooltip("Human-readable scenario name (used in CSV filename and summary)")]
        [SerializeField] private string scenarioName = "Default Benchmark";

        [Tooltip("Ordered list of phases to execute")]
        [SerializeField] private BenchmarkPhase[] phases;

        [Tooltip("Maximum average frame time in ms to consider the benchmark passed")]
        [SerializeField] private float maxAvgFrameTimeMs = 16.67f;

        public string ScenarioName
        {
            get { return scenarioName; }
        }

        public BenchmarkPhase[] Phases
        {
            get { return phases; }
        }

        public float MaxAvgFrameTimeMs
        {
            get { return maxAvgFrameTimeMs; }
        }
    }
}
