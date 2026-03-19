using System.Collections;
using System.Collections.Generic;

using Lithforge.Voxel.Block;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    ///     Places blocks in a cubic region around the player.
    ///     Measures the full pipeline impact: edit → relight → remesh → GPU upload.
    ///     Can place and then clear to measure both directions.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceBlockBulkCommand", menuName = "Lithforge/Benchmark/Commands/Place Block Bulk")]
    public sealed class PlaceBlockBulkCommand : BenchmarkCommand
    {
        /// <summary>Half-size of the cube region (e.g., 16 produces a 32x32x32 region).</summary>
        [Tooltip("Half-size of the cube region (e.g., 16 = 32x32x32 region)"), Min(1), SerializeField]
         private int halfSize = 16;

        /// <summary>If true, clears the region (sets blocks to air) instead of filling with stone.</summary>
        [Tooltip("If true, clears the region (sets to air) instead of filling"), SerializeField]
         private bool clearRegion;

        /// <summary>Offset from the player position to the center of the placed region.</summary>
        [Tooltip("Offset from player position to place the region center"), SerializeField]
         private Vector3 offset = new(0f, 0f, 32f);

        /// <summary>Reusable scratch list for dirtied chunk coordinates to avoid per-execution allocation.</summary>
        private readonly List<int3> _dirtiedChunks = new();

        public override IEnumerator Execute(BenchmarkContext context)
        {
            if (context.ChunkManager == null || context.PlayerTransform == null)
            {
                yield break;
            }

            Vector3 center = context.PlayerTransform.position + offset;
            int cx = Mathf.FloorToInt(center.x);
            int cy = Mathf.FloorToInt(center.y);
            int cz = Mathf.FloorToInt(center.z);

            StateId fillState = clearRegion ? StateId.Air : new StateId(1); // stone = 1

            int count = 0;
            _dirtiedChunks.Clear();

            for (int x = cx - halfSize; x <= cx + halfSize; x++)
            {
                for (int y = cy - halfSize; y <= cy + halfSize; y++)
                {
                    for (int z = cz - halfSize; z <= cz + halfSize; z++)
                    {
                        int3 worldPos = new(x, y, z);
                        context.ChunkManager.SetBlock(worldPos, fillState, _dirtiedChunks);
                        count++;
                    }
                }
            }

            UnityEngine.Debug.Log("[Benchmark] PlaceBlockBulk: " + count + " blocks " +
                                  (clearRegion ? "cleared" : "placed") +
                                  ", " + _dirtiedChunks.Count + " chunks dirtied");

            yield return null;
        }
    }
}
