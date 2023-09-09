namespace Hazelnut.Web.Diagnostics;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    Fatal,
    Notice,
}

public interface IWebLogger
{
    void Write(LogLevel logLevel, string message);
}