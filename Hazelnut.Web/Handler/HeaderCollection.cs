using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Hazelnut.Web.Handler;

public enum Connection
{
    KeepAlive,
    Close,
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ContentDisposition
{
    public string? Name;
    public string? Filename;
}

public class HeaderCollection : IEnumerable<KeyValuePair<string, string>>
{
    private readonly Dictionary<string, string> _collection = new();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _collection.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsContainsKey(string key)
    {
        return _collection.ContainsKey(key) ||
               _collection.Keys.Any(ck => ck.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) =>
        _collection.TryGetValue(key, out value);

    public string? this[string key]
    {
        get => _collection.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value != null)
                _collection[key] = value.Trim();
            else
                _collection.Remove(key);
        }
    }
    
    public string Host =>
        _collection.TryGetValue("Host", out var host)
            ? host
            : string.Empty;

    public string? UserAgent =>
        _collection.TryGetValue("User-Agent", out var userAgent)
            ? userAgent
            : null;

    public Connection Connection
    {
        get =>
            _collection.TryGetValue("Connection", out var connection)
                ? connection.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)
                    ? Connection.KeepAlive
                    : Connection.Close
                : Connection.KeepAlive;
        set => _collection["Connection"] = value switch
        {
            Connection.KeepAlive=> "Keep-Alive",
            Connection.Close => "Close",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public Encoding? ContentEncoding
    {
        get =>
            _collection.TryGetValue("Content-Encoding", out var contentEncoding)
                ? Encoding.GetEncoding(contentEncoding)
                : Encoding.UTF8;
        set => this["Content-Encoding"] = value?.EncodingName;
    }

    public long ContentLength
    {
        get =>
            _collection.TryGetValue("Content-Length", out var contentLength)
                ? long.TryParse(contentLength, out var result)
                    ? result
                    : 0
                : 0;
        set => _collection["Content-Length"] = value.ToString();
    }
    
    public ContentType? ContentType
    {
        get =>
            _collection.TryGetValue("Content-Type", out var contentType)
                ? new ContentType(contentType)
                : null;
        set => this["Content-Type"] = value?.ToString();
    }

    public string[]? AcceptEncoding
    {
        get =>
            _collection.TryGetValue("Accept-Encoding", out var acceptEncoding)
                ? acceptEncoding.Split(",").Select(e => e.Trim()).ToArray()
                : null;
        set => this["Accept-Encoding"] = value != null ? string.Join(", ", value) : null;
    }

    public DateTime? Date
    {
        get =>
            _collection.TryGetValue("Date", out var date)
                ? DateTime.Parse(date)
                : null;
        set => this["Date"] = value?.ToString("r");
    }

    public ContentDisposition? ContentDisposition
    {
        get
        {
            if (!_collection.TryGetValue("Content-Disposition", out var contentDisposition))
                return null;

            if (!contentDisposition.StartsWith("form-data;"))
                return null;

            var result = new ContentDisposition();

            var nameMatch = Regex.Match(contentDisposition, " name=\"(.*)\"");
            var filenameMatch = Regex.Match(contentDisposition, " filename=\"(.*)\"");

            var name = nameMatch.Groups.Count == 2
                ? nameMatch.Groups[1].Value
                : null;
            var filename = filenameMatch.Groups.Count == 2
                ? filenameMatch.Groups[1].Value
                : null;
            
            result.Name = name;
            result.Filename = filename;

            return result;
        }
    }
}