using System.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Abstract base for benchmark commands. Each command is a ScriptableObject
    /// with an Execute coroutine that performs one action (fly, teleport, place blocks, etc.)
    /// and yields between frames so the engine processes naturally.
    /// </summary>
    public abstract class BenchmarkCommand : ScriptableObject
    {
        /// <summary>
        /// Executes this command within the given benchmark context.
        /// Yield return null to wait one frame. Yield break to finish immediately.
        /// </summary>
        public abstract IEnumerator Execute(BenchmarkContext context);
    }
}
