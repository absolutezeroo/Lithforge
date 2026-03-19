namespace Lithforge.Network
{
    /// <summary>
    /// Named pipeline indices for the Unity Transport network driver.
    /// Each constant maps to a configured pipeline in NetworkDriverWrapper.
    /// </summary>
    public static class PipelineId
    {
        /// <summary>
        /// Fire-and-forget delivery with no ordering or reliability guarantees.
        /// </summary>
        public const int Unreliable = 0;

        /// <summary>
        /// Unordered delivery that drops stale packets (newest-only semantics).
        /// </summary>
        public const int UnreliableSequenced = 1;

        /// <summary>
        /// Reliable, ordered delivery with automatic retransmission.
        /// </summary>
        public const int ReliableSequenced = 2;

        /// <summary>
        /// Reliable delivery with fragmentation support for large payloads (e.g., chunk data).
        /// </summary>
        public const int FragmentedReliable = 3;

        /// <summary>
        /// Total number of configured transport pipelines.
        /// </summary>
        public const int Count = 4;
    }
}
