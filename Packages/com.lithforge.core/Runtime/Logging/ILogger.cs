namespace Lithforge.Core.Logging
{
    /// <summary>
    /// Tier 1 logging interface. Tier 3 provides a Unity implementation
    /// that delegates to UnityEngine.Debug.Log.
    /// </summary>
    public interface ILogger
    {
        public void Log(LogLevel level, string message);

        public void LogDebug(string message);

        public void LogInfo(string message);

        public void LogWarning(string message);

        public void LogError(string message);
    }
}
