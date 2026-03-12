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
        private readonly long _startTicks;
        private readonly double _budgetTicks;

        public FrameBudget(float budgetMs)
        {
            _startTicks = Stopwatch.GetTimestamp();
            _budgetTicks = budgetMs * (Stopwatch.Frequency / 1000.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExhausted()
        {
            return (Stopwatch.GetTimestamp() - _startTicks) >= _budgetTicks;
        }
    }
}