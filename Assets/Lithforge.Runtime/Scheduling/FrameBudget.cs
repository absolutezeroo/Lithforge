using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Lightweight time-budget tracker for frame-capped polling loops.
    /// Instantiated as a local variable at the start of a PollCompleted call.
    /// Uses Stopwatch.GetTimestamp() for zero allocation, sub-microsecond precision.
    /// Owner: call stack. Lifetime: single method call.
    /// </summary>
    public struct FrameBudget
    {
        /// <summary>Stopwatch tick count at the moment this budget was created.</summary>
        private readonly long _startTicks;

        /// <summary>Budget duration converted to Stopwatch ticks for zero-division-free comparison.</summary>
        private readonly double _budgetTicks;

        /// <summary>Creates a new time budget with the given millisecond allowance.</summary>
        public FrameBudget(float budgetMs)
        {
            _startTicks = Stopwatch.GetTimestamp();
            _budgetTicks = budgetMs * (Stopwatch.Frequency / 1000.0);
        }

        /// <summary>Returns true if the elapsed time since creation has exceeded the budget.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExhausted()
        {
            return (Stopwatch.GetTimestamp() - _startTicks) >= _budgetTicks;
        }
    }
}