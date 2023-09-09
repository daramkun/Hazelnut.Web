using System.IO.Compression;
using System.Net;
using System.Net.Mime;
using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public enum UnityBuiltCompression
{
    None,
    Gzip,
    Brotli,
}

public class UnityWebRequestHandler : IRequestHandler
{
    private readonly string _rootDirectory;
    private readonly UnityBuiltCompression _builtCompression;
    
    public string Location { get; set; } = "/";
    public IAuthorizeMethod? AuthorizeMethod { get; set; } = null;

    public UnityWebRequestHandler(string rootDirectory, UnityBuiltCompression buildCompression)
    {
        _rootDirectory = rootDirectory;
        _builtCompression = buildCompression;
    }

    public async ValueTask OnRequestAsync(Request request, Response response)
    {
        var queryString = request.QueryString.Length > 0 && request.QueryString[0] == '/'
            ? request.QueryString[1..]
            : request.QueryString;
        
        var filename = Path.Combine(_rootDirectory, queryString);
        if (!File.Exists(filename))
        {
            filename = Path.Combine(filename, "index.html");
            if (!File.Exists(filename))
                filename = null; 
            if (filename == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return;
            }
        }

        var isGzip = filename.EndsWith(".gz");
        var isBrotli = filename.EndsWith(".br");
        var isUnityWeb = filename.EndsWith(".unityweb");

        var gzipAcceptable = request.Headers.AcceptEncoding?.Contains("gzip") == true;
        var brotliAcceptable = request.Headers.AcceptEncoding?.Contains("br") == true;

        Stream? sourceStream = null;
        if ((isGzip && gzipAcceptable) || (isUnityWeb && gzipAcceptable && _builtCompression == UnityBuiltCompression.Gzip))
        {
            response.Headers["Content-Encoding"] = "gzip";
            response.Headers.ContentType = new ContentType(GetMimeContentType(Path.GetExtension(Path.GetFileNameWithoutExtension(filename))));
            sourceStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        }
        else if ((isBrotli && brotliAcceptable) || (isUnityWeb && brotliAcceptable && _builtCompression == UnityBuiltCompression.Brotli))
        {
            response.Headers["Content-Encoding"] = "br";
            response.Headers.ContentType = new ContentType(GetMimeContentType(Path.GetExtension(Path.GetFileNameWithoutExtension(filename))));
            sourceStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        }
        else if ((isGzip && !gzipAcceptable) ||
                 (isUnityWeb && !gzipAcceptable && _builtCompression == UnityBuiltCompression.Gzip))
        {
            response.Headers["Content-Encoding"] = "gzip";
            response.Headers.ContentType = new ContentType(GetMimeContentType(Path.GetExtension(Path.GetFileNameWithoutExtension(filename))));
            sourceStream = new GZipStream(new FileStream(filename, FileMode.Open, FileAccess.Read), CompressionMode.Decompress);
        }
        else if ((isBrotli && !brotliAcceptable) ||
                 (isUnityWeb && !brotliAcceptable && _builtCompression == UnityBuiltCompression.Brotli))
        {
            response.Headers["Content-Encoding"] = "gzip";
            response.Headers.ContentType = new ContentType(GetMimeContentType(Path.GetExtension(Path.GetFileNameWithoutExtension(filename))));
            sourceStream = new GZipStream(new FileStream(filename, FileMode.Open, FileAccess.Read), CompressionMode.Decompress);
        }
        else
        {
            response.Headers["Content-Encoding"] = null;
            response.Headers.ContentType = new ContentType(GetMimeContentType(Path.GetExtension(filename)));
            sourceStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        }

        response.StatusCode = HttpStatusCode.OK;
        await using var responseStream = response.OpenStream();
        await sourceStream.CopyToAsync(responseStream);
        await sourceStream.DisposeAsync();
    }

    private static string GetMimeContentType(string ext) =>
        ext switch
        {
            ".html" => "text/html",
            ".json" => "text/json",
            ".wasm" => "application/wasm",
            ".css" => "text/css",
            ".png" => "image/png",
            ".ico" => "image/ico",
            _ => "application/octet-stream"
        };
}