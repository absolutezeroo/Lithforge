namespace Lithforge.Core.Logging
{
    /// <summary>
    /// Severity threshold for log messages, ordered from least to most severe.
    /// Loggers filter messages below their configured level.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Verbose output for development diagnostics. Stripped in release builds.</summary>
        Debug,

        /// <summary>Normal operational messages (startup phases, content loading, etc.).</summary>
        Info,

        /// <summary>Recoverable problems that may indicate misconfiguration or degraded behavior.</summary>
        Warning,

        /// <summary>Failures that prevent a subsystem from functioning correctly.</summary>
        Error,
    }
}
