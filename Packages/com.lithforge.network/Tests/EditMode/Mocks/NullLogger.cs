using Lithforge.Core.Logging;

namespace Lithforge.Network.Tests.Mocks
{
    /// <summary>Logger that discards all messages, for test isolation.</summary>
    internal sealed class NullLogger : ILogger
    {
        /// <summary>Discards the log message.</summary>
        public void Log(LogLevel level, string message)
        {
        }

        /// <summary>Discards the debug message.</summary>
        public void LogDebug(string message)
        {
        }

        /// <summary>Discards the info message.</summary>
        public void LogInfo(string message)
        {
        }

        /// <summary>Discards the warning message.</summary>
        public void LogWarning(string message)
        {
        }

        /// <summary>Discards the error message.</summary>
        public void LogError(string message)
        {
        }
    }
}
