using System;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Thrown when <see cref="SubsystemTopologicalSorter"/> detects a cycle
    ///     in the subsystem dependency graph.
    /// </summary>
    public sealed class SubsystemCircularDependencyException : Exception
    {
        public SubsystemCircularDependencyException(string message) : base(message)
        {
        }
    }
}
