using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System;

namespace Hazelnut.Web.IO;

internal class HttpConnection : IDisposable
{
    private readonly Stream _baseStream;
    private readonly MemoryStream _tempBuffer;

    private readonly byte[] _readBuffer;
    private readonly StringBuilder _returnBuffer;

    public Stream BaseStream => _baseStream;
    
    public EndPoint? LocalEndPoint { get; private set; }
    public EndPoint? RemoteEndPoint { get; private set; }
    
    public HttpConnection(Stream stream, EndPoint? localEndPoint, EndPoint? remoteEndPoint)
    {
        _baseStream = stream;
        _tempBuffer = new MemoryStream();

        _readBuffer = new byte[4096];
        _returnBuffer = new StringBuilder(4096);

        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
    }
    
    public void Dispose()
    {
        _tempBuffer.Dispose();
        _baseStream.Dispose();
    }

    public ValueTask<string?> ReadToSpaceAsync(CancellationToken cancellationToken) =>
        ReadToTextAsync(" ", cancellationToken);
    public ValueTask<string?> ReadToNextLineAsync(CancellationToken cancellationToken) =>
        ReadToTextAsync("\r\n", cancellationToken);
    public ValueTask<string?> ReadToColonAsync(CancellationToken cancellationToken) =>
        ReadToTextAsync(":", cancellationToken);

    public async ValueTask<int> ReadToLength(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
    {
        if (buffer.Length < offset + length)
            throw new ArgumentOutOfRangeException(nameof(length));
        
        var totalRead = 0;
        while (_tempBuffer.Length != 0 && totalRead != length)
        {
            var read = await _tempBuffer.ReadAsync(new Memory<byte>(buffer, offset + totalRead, length - totalRead), cancellationToken);
            totalRead += read;
        }

        while (totalRead != length)
        {
            var read = await _baseStream.ReadAsync(new Memory<byte>(buffer, offset + totalRead, length - totalRead), cancellationToken);
            totalRead += read;
        }

        return totalRead;
    }

    public async ValueTask<int> ReadToLength(Stream stream, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        var totalRead = 0;
        while (_tempBuffer.Length != 0 && totalRead != length)
        {
            var read = await _tempBuffer.ReadAsync(new Memory<byte>(buffer, 0, (int)Math.Min(length - totalRead, buffer.Length)), cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;
        }

        while (totalRead != length)
        {
            var read = await _baseStream.ReadAsync(new Memory<byte>(buffer, 0, (int)Math.Min(length - totalRead, buffer.Length)), cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;
        }

        return totalRead;
    }

    private async ValueTask<string?> ReadToTextAsync(string text, CancellationToken cancellationToken)
    {
        _returnBuffer.Clear();

        int readLength;
        
        while (_tempBuffer.Length != 0)
        {
            readLength = await _tempBuffer.ReadAsync(_readBuffer, cancellationToken);
            if (readLength == 0)
                return null;

            var state = Buffering(readLength, text);
            if (state != null)
                return state;
        }

        // _tempBuffer에 가지고 있던 데이터를 _returnBuffer에 모조리 때려부었으니 _tempBuffer는 비워줌
        _tempBuffer.SetLength(0);

        do
        {
            readLength = await _baseStream.ReadAsync(_readBuffer, cancellationToken);
            if (readLength == 0)
                return null;
            
            var state = Buffering(readLength, text);
            if (state != null)
                return state;
        } while (true);
    }

    private string? Buffering(int readLength, string targetText)
    {
        for (var i = 0; i < readLength; ++i)
        {
            _returnBuffer.Append((char)_readBuffer[i]);

            if (_returnBuffer.Length < targetText.Length)
                continue;
            
            var found = true;
            for (var j = 0; j < targetText.Length && found; ++j)
            {
                if (targetText[targetText.Length - j - 1] != _returnBuffer[_returnBuffer.Length - j - 1])
                    found = false;
            }

            if (!found)
                continue;

            _returnBuffer.Remove(_returnBuffer.Length - targetText.Length, targetText.Length);
                
            var position = _tempBuffer.Position;
            var bufferingIndex = i + 1;
            _tempBuffer.Write(_readBuffer, bufferingIndex, _readBuffer.Length - bufferingIndex);
            _tempBuffer.Position = position;
                
            return _returnBuffer.ToString();
        }

        return null;
    }

    public async ValueTask WriteAsync(byte[] data, int offset, int count, CancellationToken cancellationToken) =>
        await _baseStream.WriteAsync(data.AsMemory(offset, count), cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        await _baseStream.WriteAsync(data, cancellationToken);
}