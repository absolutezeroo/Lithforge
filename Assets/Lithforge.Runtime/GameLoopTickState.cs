using Lithforge.Runtime.Input;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;

using UnityEngine;

namespace Lithforge.Runtime
{
    /// <summary>
    ///     Groups the fixed-tick simulation references injected into GameLoop.
    /// </summary>
    public sealed class GameLoopTickState
    {
        public IWorldSimulation WorldSimulation { get; set; }

        public InputSnapshotBuilder InputSnapshotBuilder { get; set; }

        public PlayerPhysicsBody PlayerPhysicsBody { get; set; }

        public Transform PlayerTransform { get; set; }
    }
}
