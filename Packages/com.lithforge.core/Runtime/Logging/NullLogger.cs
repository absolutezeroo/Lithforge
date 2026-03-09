namespace Lithforge.Core.Logging
{
    /// <summary>
    /// No-op logger for use in tests or when logging is not needed.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Log(LogLevel level, string message)
        {
        }

        public void LogDebug(string message)
        {
        }

        public void LogInfo(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message)
        {
        }
    }
}
