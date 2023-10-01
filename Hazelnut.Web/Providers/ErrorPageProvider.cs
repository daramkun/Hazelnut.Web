using System.Net;
using System.Text;

namespace Hazelnut.Web.Providers;

public class ErrorPageProvider
{
    public static ErrorPageProvider Default { get; } = new();
    
    private readonly string _pageTemplate = @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>{0} {1}</title>
</head>
<body style=""text-align: center;"">
<h1>{0} {1}</h1>
<hr />
<p>Keeps</p>
</body>
</html>
";
    
    public virtual Stream Provide(HttpStatusCode statusCode)
    {
        var page = string.Format(_pageTemplate, (int)statusCode, StatusCodeToString(statusCode));
        var pageBytes = Encoding.UTF8.GetBytes(page);
        return new MemoryStream(pageBytes);
    }

    internal static string StatusCodeToString(HttpStatusCode statusCode)
    {
        var builder = new StringBuilder(statusCode.ToString());

        for (var i = 0; i < builder.Length; ++i)
        {
            if (char.IsUpper(builder[i]) && i != 0)
                builder.Insert(i++, ' ');
        }
        
        return builder.ToString();
    }
}