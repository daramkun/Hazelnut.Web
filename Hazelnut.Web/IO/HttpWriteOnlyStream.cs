using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Hazelnut.Web.Handler;

namespace Hazelnut.Web.IO;

internal class HttpWriteOnlyStream : Stream
{
    private readonly MemoryStream _buffer = new();
    private readonly Response _response;
    private bool _flushed = false;

    public HttpWriteOnlyStream(Response response)
    {
        _response = response;
    }

    protected override void Dispose(bool disposing)
    {
        Flush();
        base.Dispose(disposing);
        _buffer.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await FlushAsync();
        await base.DisposeAsync();
        await _buffer.DisposeAsync();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _buffer.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush()
    {
        FlushAsync(default).GetAwaiter().GetResult();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_flushed)
            return;

        var connection = _response.Owner;
        if (connection == null)
            return;

        var headerBytes = GenerateHttpHeader(_buffer.Length);
        await connection.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken);

        var bodyBytes = _buffer.ToArray();
        await connection.WriteAsync(bodyBytes, 0, bodyBytes.Length, cancellationToken);

        _flushed = true;
    }

    private byte[] GenerateHttpHeader(long contentLength)
    {
        var builder = new StringBuilder();
        builder.Append($"HTTP/{_response.HttpVersion.Major}.{_response.HttpVersion.Minor} {(int)_response.StatusCode} {_response.StatusCode.ToString()}\r\n");

        _response.Headers.ContentLength = contentLength;
        if (!_response.Headers.IsContainsKey("Date"))
            _response.Headers.Date = DateTime.UtcNow;
        foreach (var (key, value) in _response.Headers)
            builder.Append($"{key}: {value}\r\n");
        builder.Append("\r\n");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotImplementedException();
    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}