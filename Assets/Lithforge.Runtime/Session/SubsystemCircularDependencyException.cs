using System;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Thrown when <see cref="SubsystemTopologicalSorter"/> detects a cycle
    ///     in the subsystem dependency graph.
    /// </summary>
    public sealed class SubsystemCircularDependencyException : Exception
    {
        /// <summary>Creates a new exception with the given cycle description message.</summary>
        public SubsystemCircularDependencyException(string message) : base(message)
        {
        }
    }
}
