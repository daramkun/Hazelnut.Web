using System.Diagnostics;

namespace Hazelnut.Web.Diagnostics;

public class DefaultWebLogger : IWebLogger
{
    public void Write(LogLevel logLevel, string message)
    {
        message = $"[{logLevel}] {message}";
        if (logLevel is LogLevel.Error or LogLevel.Fatal)
        {
            Debug.WriteLine(message);
            Console.Error.WriteLine(message);
        }
        else
        {
            Debug.WriteLine(message);
            Console.Out.WriteLine(message);
        }
    }
}