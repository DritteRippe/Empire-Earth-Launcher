using System;

namespace Empire_Earth_Launcher
{
    public enum LogLevel
    {
        Info, Warning, Error
    }

    /// <summary>
    /// Destination of the launcher's log messages.
    /// </summary>
    /// <remarks>
    /// There is no global logger: <see cref="Program"/> (the composition root) creates one and passes it to
    /// every class that logs, through its constructor or, for controls created by the WinForms designer,
    /// through their Initialize method.
    /// </remarks>
    public interface ILogger
    {
        /// <param name="level">Severity of the message.</param>
        /// <param name="message">Text of the message.</param>
        /// <param name="exception">Optional; its type, message, inner exceptions and stack trace are logged.</param>
        void Log(LogLevel level, string message, Exception exception = null);
    }

    public static class LoggerExtensions
    {
        public static void Info(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Info, message);
        }

        public static void Warning(this ILogger logger, string message, Exception exception = null)
        {
            logger.Log(LogLevel.Warning, message, exception);
        }

        public static void Error(this ILogger logger, string message, Exception exception = null)
        {
            logger.Log(LogLevel.Error, message, exception);
        }
    }
}
