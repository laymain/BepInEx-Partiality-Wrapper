using System;

namespace PartialityWrapper
{
    internal static class Log
    {
        public static void Info(string msg)
        {
            UnityEngine.Debug.Log($"[{nameof(PartialityWrapper)}] {msg}");
        }

        public static void Error(string msg, Exception exception = null)
        {
            UnityEngine.Debug.LogError($"[{nameof(PartialityWrapper)}] {msg}");
            UnityEngine.Debug.LogException(exception);
        }
    }
}
