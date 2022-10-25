using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Utility
{
    /// <summary>
    /// Customized logging system for URC. This class is used to log messages to the console based on logging level desired.
    /// </summary>
    public static class Logging
    {
        // The current level of logging set
        private static LoggingLevel m_loggingLevel = LoggingLevel.Dev;

        /// <summary>
        /// Sets the current logging level
        /// </summary>
        /// <param name="level">new level</param>
        public static void SetLoggingLevel(LoggingLevel level)
        {
            m_loggingLevel = level;
            Log("Logging level has been set to " + level.ToString(), LoggingLevel.Dev);
        }

        /// <summary>
        /// Logs a message to the console
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="level">The level of log</param>
        public static void Log(string message, LoggingLevel level)
        {
            if (m_loggingLevel >= level)
            {
                Debug.Log("[URC] " + message);
            }
        }
    }

    /// <summary>
    /// Different levels of logging possible
    /// </summary>
    public enum LoggingLevel
    {
        None,           // Nothing will be logged
        Critical,       // Only critical errors will be logged
        Dev             // Full logging for development
    }
}