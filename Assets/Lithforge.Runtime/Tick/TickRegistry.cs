using System.Collections.Generic;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    ///     Ordered list of ITickable systems. Registered at startup; iterated inside
    ///     GameLoop's tick loop. Registration happens once — no per-frame allocation.
    /// </summary>
    public sealed class TickRegistry
    {
        private readonly List<ITickable> _tickables = new();

        public void Register(ITickable tickable)
        {
            _tickables.Add(tickable);
        }

        /// <summary>
        ///     Runs one fixed tick across all registered systems in registration order.
        /// </summary>
        public void TickAll(float tickDt)
        {
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(tickDt);
            }
        }
    }
}
