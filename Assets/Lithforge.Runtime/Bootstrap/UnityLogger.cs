namespace Lithforge.Runtime.Bootstrap
{
    internal sealed class UnityLogger : Lithforge.Core.Logging.ILogger
    {
        public void Log(Lithforge.Core.Logging.LogLevel level, string message)
        {
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
            UnityEngine.Debug.Log($"[DEBUG] {message}");
        }

        public void LogInfo(string message)
        {
            UnityEngine.Debug.Log($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
