using System.IO.Compression;
using System.Net;
using System.Net.Mime;
using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public class FileRequestHandler : IRequestHandler
{
    private readonly string _rootDirectory;
    private readonly IEnumerable<string> _indexFileNames;

    public string Location { get; set; } = "/";
    public IAuthorizeMethod? AuthorizeMethod { get; set; } = null;
    public MimeContentTypes MimeContentTypes { get; set; } = new();
    public bool UseCompression { get; set; } = true;

    public FileRequestHandler(string rootDirectory, params string[] indexFileNames)
    {
        _rootDirectory = rootDirectory;
        _indexFileNames = indexFileNames.Length == 0
            ? new[] { "index.html", "index.htm", "index.xhtml" }
            : indexFileNames;
    }

    public async ValueTask OnRequestAsync(Request request, Response response)
    {
        var queryString = (request.QueryString.Length > 0 && request.QueryString[0] == '/')
            ? request.QueryString[1..]
            : request.QueryString;
        
        var filename = Path.Combine(_rootDirectory, queryString);
        if (!File.Exists(filename))
        {
            filename = _indexFileNames
                .Select(indexFileName => Path.Combine(_rootDirectory, queryString, indexFileName))
                .FirstOrDefault(File.Exists);
            if (filename == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return;
            }
        }

        try
        {
            var contentType = MimeContentTypes.FindMimeType(filename);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.ContentType = new ContentType(contentType);

            var requestAcceptEncoding = request.Headers.AcceptEncoding;

            var availableAcceptEncoding =
                requestAcceptEncoding?.FirstOrDefault(e =>
                    e.Equals("gzip", StringComparison.OrdinalIgnoreCase) ||
                    e.Equals("deflate", StringComparison.OrdinalIgnoreCase) ||
                    e.Equals("br", StringComparison.OrdinalIgnoreCase))?.ToLower();
            if (!UseCompression || 
                contentType.StartsWith("audio") || contentType.StartsWith("video") || contentType.StartsWith("font"))
                availableAcceptEncoding = null;

            if (availableAcceptEncoding != null)
                response.Headers["Content-Encoding"] = availableAcceptEncoding;

            await using var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            await using var responseStream = response.OpenStream();
            if (availableAcceptEncoding != null)
            {
                await using Stream encodingStream = availableAcceptEncoding switch
                {
                    "gzip" => new GZipStream(responseStream, CompressionLevel.Optimal, true),
                    "deflate" => new DeflateStream(responseStream, CompressionLevel.Optimal, true),
                    "br" => new BrotliStream(responseStream, CompressionLevel.Optimal, true),
                    _ => throw new InvalidOperationException()
                };
                
                await fileStream.CopyToAsync(encodingStream);
                await encodingStream.FlushAsync();
            }
            else
            {
                await fileStream.CopyToAsync(responseStream);
            }
        }
        catch (IOException)
        {
            response.StatusCode = HttpStatusCode.Forbidden;
        }
    }
}