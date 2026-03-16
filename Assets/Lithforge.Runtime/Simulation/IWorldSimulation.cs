namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Drives all per-tick simulation systems. Encapsulates the tick registry,
    /// player physics, and any future tick-rate systems (entity AI, redstone, etc.).
    /// In singleplayer, <see cref="WorldSimulation"/> owns the tick loop.
    /// A future dedicated server would call <see cref="Tick"/> from its own loop.
    /// </summary>
    public interface IWorldSimulation
    {
        /// <summary>
        /// Advances the simulation by one fixed time step.
        /// Processes player physics, ticks all registered systems, and increments
        /// the server tick counter.
        /// </summary>
        public void Tick(float tickDt);

        /// <summary>
        /// Current server tick number. Monotonically increasing, starts at 1.
        /// Tick 0 is reserved as the empty sentinel in CommandRingBuffer.
        /// </summary>
        public uint CurrentTick { get; }
    }
}
