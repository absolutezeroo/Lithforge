namespace Lithforge.Core.Logging
{
    /// <summary>
    /// Tier 1 logging interface. Tier 3 provides a Unity implementation
    /// that delegates to UnityEngine.Debug.Log.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Logs a message at the specified severity level.</summary>
        public void Log(LogLevel level, string message);

        /// <summary>Logs a verbose diagnostic message (stripped in release builds).</summary>
        public void LogDebug(string message);

        /// <summary>Logs a normal operational message.</summary>
        public void LogInfo(string message);

        /// <summary>Logs a recoverable problem or misconfiguration warning.</summary>
        public void LogWarning(string message);

        /// <summary>Logs a failure that prevents a subsystem from functioning.</summary>
        public void LogError(string message);
    }
}
