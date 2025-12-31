// ============================================================================
// Nightflow - Conditional Logging Utility
// Provides debug logging that can be stripped from production builds
// ============================================================================

using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nightflow.Utilities
{
    /// <summary>
    /// Conditional logging utility for Nightflow.
    ///
    /// Usage:
    /// - Log.Info("message") - General information (stripped in release builds)
    /// - Log.Warn("message") - Warnings (always included)
    /// - Log.Error("message") - Errors (always included)
    /// - Log.Verbose("message") - Verbose debug info (stripped unless NIGHTFLOW_VERBOSE defined)
    ///
    /// Configuration:
    /// - NIGHTFLOW_DEBUG: Define to enable Info logging (auto-enabled in Unity Editor)
    /// - NIGHTFLOW_VERBOSE: Define to enable Verbose logging
    /// - Production builds: Only Warn and Error are included
    ///
    /// Benefits:
    /// - Zero overhead in production builds (methods are completely stripped)
    /// - Consistent log formatting with [Nightflow] prefix
    /// - Optional context object for clickable console links
    /// </summary>
    public static class Log
    {
        private const string Prefix = "[Nightflow] ";

        // =====================================================================
        // INFO - General debug information
        // Stripped from release builds unless NIGHTFLOW_DEBUG is defined
        // =====================================================================

        /// <summary>
        /// Logs general information. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        /// <summary>
        /// Logs general information with context. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void Info(string message, Object context)
        {
            Debug.Log(Prefix + message, context);
        }

        /// <summary>
        /// Logs formatted information. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void InfoFormat(string format, params object[] args)
        {
            Debug.LogFormat(Prefix + format, args);
        }

        // =====================================================================
        // VERBOSE - Detailed debug information
        // Only included when NIGHTFLOW_VERBOSE is explicitly defined
        // =====================================================================

        /// <summary>
        /// Logs verbose debug information. Only included when NIGHTFLOW_VERBOSE is defined.
        /// </summary>
        [Conditional("NIGHTFLOW_VERBOSE")]
        public static void Verbose(string message)
        {
            Debug.Log(Prefix + "[Verbose] " + message);
        }

        /// <summary>
        /// Logs verbose debug information with context.
        /// </summary>
        [Conditional("NIGHTFLOW_VERBOSE")]
        public static void Verbose(string message, Object context)
        {
            Debug.Log(Prefix + "[Verbose] " + message, context);
        }

        // =====================================================================
        // WARN - Warnings (always included)
        // =====================================================================

        /// <summary>
        /// Logs a warning. Always included in builds.
        /// </summary>
        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        /// <summary>
        /// Logs a warning with context. Always included in builds.
        /// </summary>
        public static void Warn(string message, Object context)
        {
            Debug.LogWarning(Prefix + message, context);
        }

        /// <summary>
        /// Logs a formatted warning. Always included in builds.
        /// </summary>
        public static void WarnFormat(string format, params object[] args)
        {
            Debug.LogWarningFormat(Prefix + format, args);
        }

        // =====================================================================
        // ERROR - Errors (always included)
        // =====================================================================

        /// <summary>
        /// Logs an error. Always included in builds.
        /// </summary>
        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }

        /// <summary>
        /// Logs an error with context. Always included in builds.
        /// </summary>
        public static void Error(string message, Object context)
        {
            Debug.LogError(Prefix + message, context);
        }

        /// <summary>
        /// Logs a formatted error. Always included in builds.
        /// </summary>
        public static void ErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(Prefix + format, args);
        }

        // =====================================================================
        // SYSTEM-SPECIFIC LOGGING
        // Prefixed with system name for easier filtering
        // =====================================================================

        /// <summary>
        /// Logs system-specific information. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void System(string systemName, string message)
        {
            Debug.Log($"{Prefix}[{systemName}] {message}");
        }

        /// <summary>
        /// Logs system-specific warning. Always included.
        /// </summary>
        public static void SystemWarn(string systemName, string message)
        {
            Debug.LogWarning($"{Prefix}[{systemName}] {message}");
        }

        /// <summary>
        /// Logs system-specific error. Always included.
        /// </summary>
        public static void SystemError(string systemName, string message)
        {
            Debug.LogError($"{Prefix}[{systemName}] {message}");
        }

        // =====================================================================
        // PERFORMANCE LOGGING
        // For timing and performance analysis
        // =====================================================================

        /// <summary>
        /// Logs performance information. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void Perf(string message)
        {
            Debug.Log(Prefix + "[Perf] " + message);
        }

        /// <summary>
        /// Logs performance timing. Stripped from release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("NIGHTFLOW_DEBUG")]
        public static void PerfTiming(string operation, float milliseconds)
        {
            Debug.Log($"{Prefix}[Perf] {operation}: {milliseconds:F2}ms");
        }
    }
}
