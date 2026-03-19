using System.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Waits until the voxel pipeline is quiescent (no pending generation or meshing)
    /// or a timeout is reached. Useful as a barrier between benchmark phases.
    /// </summary>
    [CreateAssetMenu(fileName = "WaitQuiescenceCommand", menuName = "Lithforge/Benchmark/Commands/Wait Quiescence")]
    public sealed class WaitQuiescenceCommand : BenchmarkCommand
    {
        /// <summary>Maximum seconds to wait before continuing regardless of pipeline state.</summary>
        [Tooltip("Maximum seconds to wait before continuing regardless")]
        [Min(0.1f)]
        [SerializeField] private float timeoutSeconds = 30f;

        public override IEnumerator Execute(BenchmarkContext context)
        {
            float elapsed = 0f;

            while (elapsed < timeoutSeconds)
            {
                if (context.IsPipelineQuiescent)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            UnityEngine.Debug.LogWarning(
                "[Benchmark] WaitQuiescence timed out after " +
                timeoutSeconds.ToString("F1") + "s");
        }
    }
}
