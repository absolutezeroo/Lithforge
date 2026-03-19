using Lithforge.Runtime.Tick;

using UnityEngine;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Player movement controller. After SetPhysicsBody() is called, this MonoBehaviour
    ///     becomes a thin passthrough shell — all physics runs in PlayerPhysicsBody at fixed
    ///     tick rate. The MonoBehaviour is retained for scene wiring compatibility.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>
        ///     True if the player is standing on solid ground.
        /// </summary>
        public bool OnGround
        {
            get
            {
                return PhysicsBody?.OnGround ?? false;
            }
        }

        /// <summary>
        ///     True if fly mode is active.
        /// </summary>
        public bool IsFlying
        {
            get
            {
                return PhysicsBody?.IsFlying ?? false;
            }
        }

        /// <summary>
        ///     True if noclip is active (only meaningful while flying).
        /// </summary>
        public bool IsNoclip
        {
            get
            {
                return PhysicsBody?.IsNoclip ?? false;
            }
        }

        /// <summary>
        ///     True if the player's feet are in water.
        /// </summary>
        public bool IsInWater
        {
            get
            {
                return PhysicsBody?.IsInWater ?? false;
            }
        }

        /// <summary>
        ///     True if the player's eyes are submerged in water.
        /// </summary>
        public bool IsSubmerged
        {
            get
            {
                return PhysicsBody?.IsSubmerged ?? false;
            }
        }

        /// <summary>
        ///     Current fly speed in blocks per second.
        /// </summary>
        public float FlySpeed
        {
            get
            {
                return PhysicsBody?.FlySpeed ?? 10f;
            }
        }

        /// <summary>
        ///     Returns the physics body for direct access by GameLoop/Bootstrap.
        /// </summary>
        public PlayerPhysicsBody PhysicsBody { get; private set; }

        /// <summary>
        ///     Programmatically sets fly mode, noclip, and fly speed.
        ///     Used by BenchmarkRunner for automated fly benchmarks.
        /// </summary>
        public void SetFlyMode(bool fly, bool noclip, float speed)
        {
            if (PhysicsBody != null)
            {
                PhysicsBody.SetFlyMode(fly, noclip, speed);
            }
        }

        /// <summary>No-op initialization retained for backwards compatibility with scene wiring.</summary>
        public void Initialize()
        {
        }

        /// <summary>
        ///     Wires the physics body and disables this MonoBehaviour's Update().
        ///     All movement now runs through PlayerPhysicsBody at fixed tick rate.
        /// </summary>
        public void SetPhysicsBody(PlayerPhysicsBody body)
        {
            PhysicsBody = body;
            enabled = false;
        }
    }
}
