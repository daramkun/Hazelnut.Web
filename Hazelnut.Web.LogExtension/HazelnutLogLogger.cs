using Hazelnut.Web.Diagnostics;

namespace Hazelnut.Web.LogExtension;

public class HazelnutLogLogger : IWebLogger
{
    private readonly Hazelnut.Log.ILogger _logger;

    public HazelnutLogLogger(Hazelnut.Log.ILogger logger)
    {
        _logger = logger;
    }
    
    public void Write(LogLevel logLevel, string message) =>
        _logger.Write(GetLogLevel(logLevel), message);

    private static Log.LogLevel GetLogLevel(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Debug => Log.LogLevel.Debug,
            LogLevel.Info => Log.LogLevel.Information,
            LogLevel.Warn => Log.LogLevel.Warning,
            LogLevel.Error => Log.LogLevel.Error,
            LogLevel.Fatal => Log.LogLevel.Fatal,
            LogLevel.Notice => Log.LogLevel.Notice,
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
}