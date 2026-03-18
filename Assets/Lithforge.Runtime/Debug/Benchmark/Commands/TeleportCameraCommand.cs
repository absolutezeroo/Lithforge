using System.Collections;

using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    ///     Teleports the player to a specified world position.
    ///     Useful for testing chunk load/unload behavior after large position changes.
    /// </summary>
    [CreateAssetMenu(fileName = "TeleportCameraCommand", menuName = "Lithforge/Benchmark/Commands/Teleport Camera")]
    public sealed class TeleportCameraCommand : BenchmarkCommand
    {
        [Tooltip("Use relative offset from current position instead of absolute position"), SerializeField]
         private bool relativeOffset = true;

        [Tooltip("Target position or offset in world coordinates"), SerializeField]
         private Vector3 position = new(2000f, 0f, 0f);

        public override IEnumerator Execute(BenchmarkContext context)
        {
            if (context.PlayerTransform == null)
            {
                yield break;
            }

            if (relativeOffset)
            {
                context.PlayerTransform.position += position;
            }
            else
            {
                context.PlayerTransform.position = position;
            }

            // Wait one frame for the position change to take effect
            yield return null;
        }
    }
}
