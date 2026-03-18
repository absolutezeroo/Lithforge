using Lithforge.Core.Logging;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Bridges <see cref="Lithforge.Core.Logging.ILogger" /> to Unity's Debug.Log system,
    ///     applying level filtering and appropriate severity routing (Log vs LogWarning vs LogError).
    /// </summary>
    internal sealed class UnityLogger : ILogger
    {
        private readonly LogLevel _minLevel;

        public UnityLogger(LogLevel minLevel = LogLevel.Debug)
        {
            _minLevel = minLevel;
        }

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

        public void LogDebug(string message)
        {
            if (_minLevel > LogLevel.Debug)
            {
                return;
            }

            UnityEngine.Debug.Log($"[DEBUG] {message}");
        }

        public void LogInfo(string message)
        {
            if (_minLevel > LogLevel.Info)
            {
                return;
            }

            UnityEngine.Debug.Log($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            if (_minLevel > LogLevel.Warning)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
