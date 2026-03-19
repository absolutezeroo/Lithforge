using System.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Flies the player forward at a configurable speed for a given duration.
    /// Uses the camera's horizontal forward direction to ensure reproducible paths.
    /// </summary>
    [CreateAssetMenu(fileName = "FlyForwardCommand", menuName = "Lithforge/Benchmark/Commands/Fly Forward")]
    public sealed class FlyForwardCommand : BenchmarkCommand
    {
        /// <summary>Fly speed in blocks per second.</summary>
        [Tooltip("Fly speed in blocks per second")]
        [Min(1f)]
        [SerializeField] private float speed = 50f;

        /// <summary>Duration of the fly-forward movement in seconds.</summary>
        [Tooltip("Duration in seconds")]
        [Min(0.1f)]
        [SerializeField] private float duration = 10f;

        public override IEnumerator Execute(BenchmarkContext context)
        {
            if (context.PlayerTransform == null)
            {
                yield break;
            }

            // Calculate horizontal forward direction from camera
            Vector3 flyDirection = Vector3.forward;

            if (context.MainCamera != null)
            {
                Vector3 fwd = context.MainCamera.transform.forward;
                fwd.y = 0f;

                if (fwd.sqrMagnitude > 0.001f)
                {
                    flyDirection = fwd.normalized;
                }
            }

            // Enable fly mode if PlayerController is available
            if (context.PlayerController != null)
            {
                context.PlayerController.SetFlyMode(true, true, speed);
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                float dt = Time.unscaledDeltaTime;
                context.PlayerTransform.position += flyDirection * (speed * dt);

                // Lock camera orientation
                if (context.MainCamera != null)
                {
                    context.MainCamera.transform.forward = flyDirection;
                }

                elapsed += dt;
                yield return null;
            }

            // BenchmarkRunner handles ExternallyControlled flag lifecycle
        }
    }
}
