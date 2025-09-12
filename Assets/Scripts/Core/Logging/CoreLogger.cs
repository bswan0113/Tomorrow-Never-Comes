namespace Core.Logging
{
    // Assets/Scripts/Core/Logging/Logger.cs
    using UnityEngine;
    
    public static class CoreLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error, Critical }
    
        public static void Log(string message, LogLevel level = LogLevel.Info, Object context = null)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                switch (level)
                {
                    case LogLevel.Debug:
                        Debug.Log($"[DEBUG] {message}", context);
                        break;
                    case LogLevel.Info:
                        Debug.Log($"[INFO] {message}", context);
                        break;
                    case LogLevel.Warning:
                        Debug.LogWarning($"[WARNING] {message}", context);
                        break;
                    case LogLevel.Error:
                        Debug.LogError($"[ERROR] {message}", context);
                        break;
                    case LogLevel.Critical:
                        Debug.LogError($"[CRITICAL] {message} - IMMEDIATE ATTENTION NEEDED!", context);
                        break;
                }
            #endif

        }
    
        public static void LogError(string message, Object context = null) => Log(message, LogLevel.Error, context);
        public static void LogWarning(string message, Object context = null) => Log(message, LogLevel.Warning, context);
        public static void LogInfo(string message, Object context = null) => Log(message, LogLevel.Info, context);
        public static void LogDebug(string message, Object context = null) => Log(message, LogLevel.Debug, context);
        public static void LogCritical(string message, Object context = null) => Log(message, LogLevel.Critical, context);
    }
}