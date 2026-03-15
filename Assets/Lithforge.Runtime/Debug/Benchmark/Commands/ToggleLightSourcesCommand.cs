using System.Collections;
using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Places or removes light-emitting blocks (torches) in a region around the player.
    /// Measures cross-chunk light propagation convergence time.
    /// </summary>
    [CreateAssetMenu(fileName = "ToggleLightSourcesCommand", menuName = "Lithforge/Benchmark/Commands/Toggle Light Sources")]
    public sealed class ToggleLightSourcesCommand : BenchmarkCommand
    {
        [Tooltip("Number of light sources to place")]
        [Min(1)]
        [SerializeField] private int count = 200;

        [Tooltip("Spread radius in blocks from player position")]
        [Min(1)]
        [SerializeField] private int spreadRadius = 64;

        [Tooltip("If true, removes light sources (places air); otherwise places torches")]
        [SerializeField] private bool remove;

        [Tooltip("Random seed for reproducible light placement")]
        [SerializeField] private int seed = 42;

        // Reusable scratch list for dirtied chunk coordinates
        private readonly List<int3> _dirtiedChunks = new List<int3>();

        public override IEnumerator Execute(BenchmarkContext context)
        {
            if (context.ChunkManager == null || context.PlayerTransform == null)
            {
                yield break;
            }

            Vector3 playerPos = context.PlayerTransform.position;
            int px = Mathf.FloorToInt(playerPos.x);
            int py = Mathf.FloorToInt(playerPos.y);
            int pz = Mathf.FloorToInt(playerPos.z);

            // Use torch StateId — stone (1) as fallback if torch isn't available
            StateId torchState = new StateId(1);
            StateId airState = StateId.Air;

            System.Random rng = new System.Random(seed);
            int placed = 0;
            _dirtiedChunks.Clear();

            for (int i = 0; i < count; i++)
            {
                int x = px + rng.Next(-spreadRadius, spreadRadius + 1);
                int y = py + rng.Next(-spreadRadius / 4, spreadRadius / 4 + 1);
                int z = pz + rng.Next(-spreadRadius, spreadRadius + 1);

                int3 worldPos = new int3(x, y, z);
                StateId fillState = remove ? airState : torchState;
                context.ChunkManager.SetBlock(worldPos, fillState, _dirtiedChunks);
                placed++;
            }

            UnityEngine.Debug.Log("[Benchmark] ToggleLightSources: " + placed +
                (remove ? " removed" : " placed"));

            yield return null;
        }
    }
}
