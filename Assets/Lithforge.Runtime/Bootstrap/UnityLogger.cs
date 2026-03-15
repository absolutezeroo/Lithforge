namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Bridges <see cref="Lithforge.Core.Logging.ILogger"/> to Unity's Debug.Log system,
    /// applying level filtering and appropriate severity routing (Log vs LogWarning vs LogError).
    /// </summary>
    internal sealed class UnityLogger : Lithforge.Core.Logging.ILogger
    {
        private readonly Lithforge.Core.Logging.LogLevel _minLevel;

        public UnityLogger(Lithforge.Core.Logging.LogLevel minLevel = Lithforge.Core.Logging.LogLevel.Debug)
        {
            _minLevel = minLevel;
        }

        public void Log(Lithforge.Core.Logging.LogLevel level, string message)
        {
            if (level < _minLevel)
            {
                return;
            }

            switch (level)
            {
                case Lithforge.Core.Logging.LogLevel.Debug:
                    UnityEngine.Debug.Log($"[DEBUG] {message}");
                    break;
                case Lithforge.Core.Logging.LogLevel.Info:
                    UnityEngine.Debug.Log($"[INFO] {message}");
                    break;
                case Lithforge.Core.Logging.LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case Lithforge.Core.Logging.LogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }

        public void LogDebug(string message)
        {
            if (_minLevel > Lithforge.Core.Logging.LogLevel.Debug)
            {
                return;
            }

            UnityEngine.Debug.Log($"[DEBUG] {message}");
        }

        public void LogInfo(string message)
        {
            if (_minLevel > Lithforge.Core.Logging.LogLevel.Info)
            {
                return;
            }

            UnityEngine.Debug.Log($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            if (_minLevel > Lithforge.Core.Logging.LogLevel.Warning)
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
