using System.Diagnostics;
using UnityEngine;

public static class PanoptesLog
{
    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Log(object message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Log(object message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Warning(object message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Warning(object message, Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Error(object message)
    {
        UnityEngine.Debug.LogError(message);
    }

    [Conditional("PANOPTES_DEBUG_LOGS")]
    public static void Error(object message, Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }
}
