namespace Lithforge.Core.Logging
{
    /// <summary>
    ///     No-op logger for use in tests or when logging is not needed.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly NullLogger Instance = new();

        /// <summary>Prevents external instantiation; use Instance instead.</summary>
        private NullLogger()
        {
        }

        /// <summary>No-op: discards the message.</summary>
        public void Log(LogLevel level, string message)
        {
        }

        /// <summary>No-op: discards the message.</summary>
        public void LogDebug(string message)
        {
        }

        /// <summary>No-op: discards the message.</summary>
        public void LogInfo(string message)
        {
        }

        /// <summary>No-op: discards the message.</summary>
        public void LogWarning(string message)
        {
        }

        /// <summary>No-op: discards the message.</summary>
        public void LogError(string message)
        {
        }
    }
}
