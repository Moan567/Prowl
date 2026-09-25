namespace Relic.Framework;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error
}

public interface ILogger
{
    void Log(LogLevel level, string message);
    void Log(LogLevel level, string format, params object[] args);
}

public static class LoggerExtensions
{
    public static void Trace(this ILogger logger, string message) => logger.Log(LogLevel.Trace, message);
    public static void Debug(this ILogger logger, string message) => logger.Log(LogLevel.Debug, message);
    public static void Info(this ILogger logger, string message) => logger.Log(LogLevel.Info, message);
    public static void Warn(this ILogger logger, string message) => logger.Log(LogLevel.Warning, message);
    public static void Error(this ILogger logger, string message) => logger.Log(LogLevel.Error, message);

    public static void Trace(this ILogger logger, string format, params object[] args) => logger.Log(LogLevel.Trace, format, args);
    public static void Debug(this ILogger logger, string format, params object[] args) => logger.Log(LogLevel.Debug, format, args);
    public static void Info(this ILogger logger, string format, params object[] args) => logger.Log(LogLevel.Info, format, args);
    public static void Warn(this ILogger logger, string format, params object[] args) => logger.Log(LogLevel.Warning, format, args);
    public static void Error(this ILogger logger, string format, params object[] args) => logger.Log(LogLevel.Error, format, args);
}
