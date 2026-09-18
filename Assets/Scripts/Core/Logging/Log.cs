using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace LastGround.Core.Logging
{
    /// <summary>
    /// Categorized logging. Info calls are compiled out of release builds (TDD_02 §15.10);
    /// warnings and errors always remain.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(LogCategory category, string message)
        {
            Debug.Log("[" + category + "] " + message);
        }

        public static void Warning(LogCategory category, string message)
        {
            Debug.LogWarning("[" + category + "] " + message);
        }

        public static void Error(LogCategory category, string message)
        {
            Debug.LogError("[" + category + "] " + message);
        }
    }
}
