using Lithforge.Core.Logging;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Bridges <see cref="Lithforge.Core.Logging.ILogger" /> to Unity's Debug.Log system,
    ///     applying level filtering and appropriate severity routing (Log vs LogWarning vs LogError).
    /// </summary>
    internal sealed class UnityLogger : ILogger
    {
        /// <summary>Minimum log level; messages below this are discarded.</summary>
        private readonly LogLevel _minLevel;

        /// <summary>Creates a logger with the specified minimum severity level.</summary>
        public UnityLogger(LogLevel minLevel = LogLevel.Debug)
        {
            _minLevel = minLevel;
        }

        /// <summary>Logs a message at the specified level if it meets the minimum threshold.</summary>
        public void Log(LogLevel level, string message)
        {
            if (level < _minLevel)
            {
                return;
            }

            switch (level)
            {
                case LogLevel.Debug:
                    UnityEngine.Debug.Log($"[DEBUG] {message}");
                    break;
                case LogLevel.Info:
                    UnityEngine.Debug.Log($"[INFO] {message}");
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }

        /// <summary>Logs a debug-level message if the minimum level allows it.</summary>
        public void LogDebug(string message)
        {
            if (_minLevel > LogLevel.Debug)
            {
                return;
            }

            UnityEngine.Debug.Log($"[DEBUG] {message}");
        }

        /// <summary>Logs an info-level message if the minimum level allows it.</summary>
        public void LogInfo(string message)
        {
            if (_minLevel > LogLevel.Info)
            {
                return;
            }

            UnityEngine.Debug.Log($"[INFO] {message}");
        }

        /// <summary>Logs a warning-level message if the minimum level allows it.</summary>
        public void LogWarning(string message)
        {
            if (_minLevel > LogLevel.Warning)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(message);
        }

        /// <summary>Logs an error-level message (always emitted regardless of minimum level).</summary>
        public void LogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
