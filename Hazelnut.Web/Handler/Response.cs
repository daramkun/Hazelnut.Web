using System.Net;
using System.Net.Mime;
using System.Text;
using Hazelnut.Web.IO;

namespace Hazelnut.Web.Handler;

public class Response
{
    private readonly WeakReference<HttpConnection> _httpConnection;

    private bool _isResponseStreamOpened = false;
    
    public Version HttpVersion { get; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public HeaderCollection Headers { get; } = new();

    internal HttpConnection? Owner => _httpConnection.TryGetTarget(out var owner) ? owner : null;

    public bool IsStreamOpened => _isResponseStreamOpened;

    internal Response(Request request, HttpConnection connection, string serverName)
    {
        _httpConnection = new WeakReference<HttpConnection>(connection);

        HttpVersion = request.HttpVersion > new Version(1, 1) ? new Version(1, 1) : request.HttpVersion;

        Headers.ContentType = new ContentType("text/html");
        Headers["Server"] = serverName;
        Headers["Cookie"] = request.Headers["Cookie"];
    }

    public Stream OpenStream()
    {
        if (_isResponseStreamOpened)
            throw new InvalidOperationException();
        _isResponseStreamOpened = true;
        return new HttpWriteOnlyStream(this);
    }

    public BinaryWriter OpenBinaryStream() => new(OpenStream());
    public TextWriter OpenTextStream() => new StreamWriter(OpenStream());
}