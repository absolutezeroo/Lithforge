using System.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Placeholder command for spawning block entities (furnaces, chests) to stress-test
    /// the block entity tick scheduler. Requires block entity system integration.
    /// Currently yields immediately — to be extended when block entity benchmarking is needed.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnBlockEntitiesCommand", menuName = "Lithforge/Benchmark/Commands/Spawn Block Entities")]
    public sealed class SpawnBlockEntitiesCommand : BenchmarkCommand
    {
        [Tooltip("Number of block entities to spawn")]
        [Min(1)]
        [SerializeField] private int count = 100;

        [Tooltip("Spread radius in blocks from player position")]
        [Min(1)]
        [SerializeField] private int spreadRadius = 32;

        public override IEnumerator Execute(BenchmarkContext context)
        {
            UnityEngine.Debug.Log("[Benchmark] SpawnBlockEntities: " + count +
                " entities (placeholder — requires block entity benchmark hooks)");
            yield return null;
        }
    }
}
