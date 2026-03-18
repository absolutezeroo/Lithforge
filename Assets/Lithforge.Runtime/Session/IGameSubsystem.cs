using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     A modular unit of session-lifetime functionality.
    ///     Subsystems are created, initialized, and disposed by <see cref="GameSession" />.
    /// </summary>
    /// <remarks>
    ///     Lifecycle: ShouldCreate → Initialize → PostInitialize → [session runs] → Shutdown → Dispose.
    ///     <para>
    ///         <see cref="Dependencies" /> declares which other subsystem types must initialize first.
    ///         <see cref="SubsystemTopologicalSorter" /> uses these to compute initialization order.
    ///         Disposal runs in reverse initialization order via <see cref="SessionLifetimeTracker" />.
    ///     </para>
    /// </remarks>
    public interface IGameSubsystem : IDisposable
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name { get; }

        /// <summary>
        ///     Subsystem types that must be initialized before this one.
        ///     Return <see cref="Array.Empty{T}()" /> if none.
        /// </summary>
        public IReadOnlyList<Type> Dependencies { get; }

        /// <summary>
        ///     Returns true if this subsystem should be created for the given session config.
        ///     Subsystems that return false are excluded from the session entirely.
        /// </summary>
        public bool ShouldCreate(SessionConfig config);

        /// <summary>
        ///     Construct owned objects and register them in the context.
        ///     Called in topological order (dependencies first).
        /// </summary>
        public void Initialize(SessionContext context);

        /// <summary>
        ///     Wire cross-cutting concerns that require other subsystems to be initialized.
        ///     Called after all subsystems have been initialized, in the same order.
        /// </summary>
        public void PostInitialize(SessionContext context);

        /// <summary>
        ///     Complete in-flight Burst jobs before disposal.
        ///     Only schedulers need to implement this; others should no-op.
        /// </summary>
        public void Shutdown();
    }
}
