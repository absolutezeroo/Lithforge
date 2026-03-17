using Lithforge.Runtime.Tick;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Generic circular buffer for interpolating remote entity states between server snapshots.
    /// Stores timestamped snapshots and provides render-time sampling with linear interpolation
    /// and bounded extrapolation. Used for remote player rendering at frame rate while server
    /// updates arrive at tick rate (30 TPS).
    /// </summary>
    public sealed class InterpolationBuffer<T> where T : struct
    {
        private const int Capacity = 32;
        private const float MaxExtrapolationSeconds = 0.25f;

        /// <summary>Interpolation delay: 2 ticks behind real-time to absorb jitter.</summary>
        public static readonly float InterpolationDelay = 2f * FixedTickRate.TickDeltaTime;

        private readonly T[] _snapshots;
        private readonly float[] _timestamps;
        private int _writeIndex;
        private int _count;

        public InterpolationBuffer()
        {
            _snapshots = new T[Capacity];
            _timestamps = new float[Capacity];
            _writeIndex = 0;
            _count = 0;
        }

        /// <summary>
        /// Pushes a new snapshot with the given server timestamp.
        /// </summary>
        public void Push(float serverTimestamp, T snapshot)
        {
            _snapshots[_writeIndex] = snapshot;
            _timestamps[_writeIndex] = serverTimestamp;
            _writeIndex = (_writeIndex + 1) % Capacity;

            if (_count < Capacity)
            {
                _count++;
            }
        }

        /// <summary>
        /// Samples the buffer at the given render time. Returns interpolation parameters:
        /// the two bracketing snapshots and the interpolation factor (0-1 for interpolation,
        /// &gt;1 for clamped extrapolation). Returns false if the buffer is empty.
        /// </summary>
        public bool Sample(float renderTime, out T from, out T to, out float alpha)
        {
            from = default;
            to = default;
            alpha = 0f;

            if (_count == 0)
            {
                return false;
            }

            float targetTime = renderTime - InterpolationDelay;

            // Find the two bracketing snapshots
            int newestIdx = (_writeIndex - 1 + Capacity) % Capacity;
            int oldestIdx = (_writeIndex - _count + Capacity) % Capacity;

            float newestTime = _timestamps[newestIdx];
            float oldestTime = _timestamps[oldestIdx];

            // Target is beyond newest: extrapolate
            if (targetTime >= newestTime)
            {
                if (_count < 2)
                {
                    from = _snapshots[newestIdx];
                    to = _snapshots[newestIdx];
                    alpha = 0f;
                    return true;
                }

                int prevIdx = (newestIdx - 1 + Capacity) % Capacity;
                from = _snapshots[prevIdx];
                to = _snapshots[newestIdx];

                float tickInterval = _timestamps[newestIdx] - _timestamps[prevIdx];

                if (tickInterval > 0.001f)
                {
                    float extrapolationTime = targetTime - newestTime;

                    // Clamp extrapolation
                    if (extrapolationTime > MaxExtrapolationSeconds)
                    {
                        extrapolationTime = MaxExtrapolationSeconds;
                    }

                    alpha = 1f + (extrapolationTime / tickInterval);
                }
                else
                {
                    alpha = 1f;
                }

                return true;
            }

            // Target is before oldest: hold at oldest
            if (targetTime <= oldestTime)
            {
                from = _snapshots[oldestIdx];
                to = _snapshots[oldestIdx];
                alpha = 0f;
                return true;
            }

            // Find bracketing pair: walk backwards from newest
            for (int i = 0; i < _count - 1; i++)
            {
                int idxB = (newestIdx - i + Capacity) % Capacity;
                int idxA = (idxB - 1 + Capacity) % Capacity;

                float timeA = _timestamps[idxA];
                float timeB = _timestamps[idxB];

                if (targetTime >= timeA && targetTime <= timeB)
                {
                    from = _snapshots[idxA];
                    to = _snapshots[idxB];
                    float interval = timeB - timeA;
                    alpha = interval > 0.001f ? (targetTime - timeA) / interval : 0f;
                    return true;
                }
            }

            // Fallback: use oldest
            from = _snapshots[oldestIdx];
            to = _snapshots[oldestIdx];
            alpha = 0f;
            return true;
        }

        /// <summary>
        /// Returns the number of snapshots currently stored.
        /// </summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>
        /// Clears all stored snapshots.
        /// </summary>
        public void Clear()
        {
            _writeIndex = 0;
            _count = 0;
        }
    }
}
