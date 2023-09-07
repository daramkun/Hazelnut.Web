using System.Net;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using Hazelnut.Web.IO;

namespace Hazelnut.Web.Handler;

[Serializable]
public class Request
{
    internal static async ValueTask<Request> ParseRequestAsync(HttpConnection connection, HttpServer server, CancellationToken cancellationToken)
    {
        var result = new Request();

        // Read Method
        var method = await connection.ReadToSpaceAsync(cancellationToken) ?? throw new IOException();
        result.Method = method switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "OPTIONS" => HttpMethod.Options,
            "CONNECT" => HttpMethod.Connect,
            "HEAD" => HttpMethod.Head,
            "TRACE" => HttpMethod.Trace,
            _ => throw new ArgumentOutOfRangeException()
        };

        // Read Query-string and Parameters
        var queryString = await connection.ReadToSpaceAsync(cancellationToken) ?? throw new IOException();
        var sharpMarkIndex = queryString.IndexOf('#');
        if (sharpMarkIndex >= 0)
        {
            result.Comment = queryString[sharpMarkIndex..];
            queryString = queryString[..sharpMarkIndex];
        }
        var questionMarkIndex = queryString.IndexOf('?');
        if (questionMarkIndex >= 0)
        {
            var parameters = queryString[questionMarkIndex..];
            queryString = queryString[..questionMarkIndex];
            ParseParameters(parameters, (Dictionary<string, string>)result.Parameters, true);
        }
        result.QueryString = queryString;
        
        // Read HTTP version
        var httpVersion = await connection.ReadToNextLineAsync(cancellationToken) ?? throw new IOException();
        if (Version.TryParse(httpVersion[5..], out var version))
            result.HttpVersion = version;

        // Read Headers
        while (await connection.ReadToColonAsync(cancellationToken) is { } key)
        {
            var value = await connection.ReadToNextLineAsync(cancellationToken);
            result.Headers[key] = value!.Trim();
        }

        // Skip Line
        await connection.ReadToNextLineAsync(cancellationToken);

        // Read Content body
        var contentLength = result.Headers.ContentLength;
        if (contentLength > 0)
        {
            var contentType = result.Headers.ContentType;
            if (contentType != null)
            {
                // Url-Encoded
                if (contentType.MediaType == "application/x-www-form-urlencoded")
                    await ParseBodyAsync(connection, result.Headers, (Dictionary<string, string>)result.Body,
                        true, cancellationToken);
                // Plain Text (Space only encoded)
                else if (contentType.MediaType == "text/plain")
                    await ParseBodyAsync(connection, result.Headers, (Dictionary<string, string>)result.Body,
                        false, cancellationToken);
                // Multi-part
                else if (contentType.MediaType == "multipart/form-data")
                    await ParseBodyAsync(connection, contentType, (Dictionary<string, string>)result.Body,
                        cancellationToken);
                else
                    throw new InvalidDataException();
            }
        }

        result.RemoteEndPoint = connection.RemoteEndPoint;

        return result;
    }

    private static void ParseParameters(string parameters, IDictionary<string, string> store, bool decode)
    {
        var splitParameters = parameters.Split('&');
        foreach (var p in splitParameters.Select(parameter => parameter.Split('=')))
            store.Add(
                decode
                    ? HttpUtility.UrlDecode(p[0])
                    : p[0].Replace('+', ' '),
                decode
                    ? HttpUtility.UrlDecode(p[1])
                    : p[1].Replace('+', ' ')
            );
    }

    private static async ValueTask ParseBodyAsync(HttpConnection connection, HeaderCollection headers,
        IDictionary<string, string> body, bool decode, CancellationToken cancellationToken)
    {
        var encoding = headers.ContentEncoding ?? Encoding.UTF8;
        var contentLength = headers.ContentLength;

        var buffer = new byte[contentLength];
        await connection.ReadToLength(buffer, 0, buffer.Length, cancellationToken);

        var postData = encoding.GetString(buffer);
        ParseParameters(postData, (Dictionary<string, string>)body, decode);
    }

    private static async Task ParseBodyAsync(HttpConnection connection, ContentType contentType,
        IDictionary<string, string> body, CancellationToken cancellationToken)
    {
        var compareBoundary = "--" + contentType.Boundary;

        while (true)
        {
            var boundary = await connection.ReadToNextLineAsync(cancellationToken);
            if (boundary == null || !boundary.StartsWith(compareBoundary))
                break;

            if (boundary.EndsWith("--"))
                break;

            var headers = new HeaderCollection();
            while (await connection.ReadToColonAsync(cancellationToken) is { } key)
            {
                var value = await connection.ReadToNextLineAsync(cancellationToken);
                headers[key] = value!.Trim();
            }

            var contentLength = headers.ContentLength;
            var contentDisposition = headers.ContentDisposition ?? throw new InvalidDataException();

            var fs = Path.Combine(
                Path.GetTempPath(),
                connection.RemoteEndPoint?.ToString() ?? "Keeps",
                contentDisposition.Filename ?? Path.GetTempFileName()
            );

            await using var fileStream = new FileStream(fs, FileMode.Create, FileAccess.Write);
            await connection.ReadToLength(fileStream, contentLength, cancellationToken);

            if (contentDisposition.Name != null)
                body[contentDisposition.Name] = fs;
            else
                File.Delete(fs);
        }
    }

    private static void AddToPostBody(IDictionary<string, string> postBody, HeaderCollection headers, MemoryStream stream)
    {
        var contentDisposition = headers["Content-Disposition"];
        if (contentDisposition == null)
            return;

        var (name, filename) = ExtractContentDisposition(contentDisposition);
        if (name == null)
            return;
        
        postBody.Add(name, filename ?? Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static (string?, string?) ExtractContentDisposition(string contentDisposition)
    {
        var nameMatch = Regex.Match(contentDisposition, "name=\"(.*)\"");
        var filenameMatch = Regex.Match(contentDisposition, "filename=\"(.*)\"");

        var name = nameMatch.Groups.Count > 1 ? nameMatch.Groups[1].Value : null;
        var filename = filenameMatch.Groups.Count > 1 ? filenameMatch.Groups[1].Value : null;

        return (name, filename);
    }
    
    public HttpMethod Method { get; private set; } = HttpMethod.Get;
    public string QueryString { get; private set; } = string.Empty;
    public Version HttpVersion { get; private set; } = new(1, 0);
    public HeaderCollection Headers { get; } = new();
    
    public IReadOnlyDictionary<string, string> Parameters { get; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Body { get; } = new Dictionary<string, string>();


    public string Comment { get; private set; } = string.Empty;
    
    public EndPoint? RemoteEndPoint { get; private set; }

    private Request() { }
}