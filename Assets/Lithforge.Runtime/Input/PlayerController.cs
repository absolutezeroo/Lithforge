using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using UnityEngine;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Player movement controller. After SetPhysicsBody() is called, this MonoBehaviour
    /// becomes a thin passthrough shell — all physics runs in PlayerPhysicsBody at fixed
    /// tick rate. The MonoBehaviour is retained for scene wiring compatibility.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private GameLoop _gameLoop;
        private PlayerPhysicsBody _physicsBody;

        /// <summary>
        /// True if the player is standing on solid ground.
        /// </summary>
        public bool OnGround
        {
            get { return _physicsBody != null ? _physicsBody.OnGround : false; }
        }

        /// <summary>
        /// True if fly mode is active.
        /// </summary>
        public bool IsFlying
        {
            get { return _physicsBody != null ? _physicsBody.IsFlying : false; }
        }

        /// <summary>
        /// True if noclip is active (only meaningful while flying).
        /// </summary>
        public bool IsNoclip
        {
            get { return _physicsBody != null ? _physicsBody.IsNoclip : false; }
        }

        /// <summary>
        /// Current fly speed in blocks per second.
        /// </summary>
        public float FlySpeed
        {
            get { return _physicsBody != null ? _physicsBody.FlySpeed : 10f; }
        }

        /// <summary>
        /// Programmatically sets fly mode, noclip, and fly speed.
        /// Used by BenchmarkRunner for automated fly benchmarks.
        /// </summary>
        public void SetFlyMode(bool fly, bool noclip, float speed)
        {
            if (_physicsBody != null)
            {
                _physicsBody.SetFlyMode(fly, noclip, speed);
            }
        }

        public void Initialize(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            GameLoop gameLoop,
            PhysicsSettings physics)
        {
            _gameLoop = gameLoop;
        }

        /// <summary>
        /// Wires the physics body and disables this MonoBehaviour's Update().
        /// All movement now runs through PlayerPhysicsBody at fixed tick rate.
        /// </summary>
        public void SetPhysicsBody(PlayerPhysicsBody body)
        {
            _physicsBody = body;
            enabled = false;
        }

        /// <summary>
        /// Returns the physics body for direct access by GameLoop/Bootstrap.
        /// </summary>
        public PlayerPhysicsBody PhysicsBody
        {
            get { return _physicsBody; }
        }
    }
}
