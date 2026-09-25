namespace Relic.Framework;

public sealed class ConsoleLogger : ILogger
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel) return;

        var color = level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        var prefix = level switch
        {
            LogLevel.Trace => "[TRC]",
            LogLevel.Debug => "[DBG]",
            LogLevel.Info => "[INF]",
            LogLevel.Warning => "[WRN]",
            LogLevel.Error => "[ERR]",
            _ => "[???]"
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"{prefix} {message}");
        Console.ForegroundColor = originalColor;
    }

    public void Log(LogLevel level, string format, params object[] args)
    {
        Log(level, string.Format(format, args));
    }
}