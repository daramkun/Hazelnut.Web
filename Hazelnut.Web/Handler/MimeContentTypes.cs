namespace Hazelnut.Web.Handler;

public class MimeContentTypes
{
    private readonly Dictionary<string, string> _mimes;

    public MimeContentTypes(params KeyValuePair<string, string>[] mimes)
    {
        _mimes = new Dictionary<string, string>(mimes.Select(kv =>
            new KeyValuePair<string, string>(kv.Key.ToLower(), kv.Value)));

        if (mimes.Length == 0)
        {
            _mimes.Add(".html", "text/html");
            _mimes.Add(".htm", "text/html");
            _mimes.Add(".css", "text/css");
            _mimes.Add(".txt", "text/plain");
            _mimes.Add(".log", "text/plain");
            _mimes.Add(".conf", "text/plain");
            _mimes.Add(".cfg", "text/plain");
            _mimes.Add(".csv", "text/csv");
            _mimes.Add(".rtf", "text/rtf");
            _mimes.Add(".xml", "text/xml");
            _mimes.Add(".json", "text/json");
            
            _mimes.Add(".js", "application/javascript");
            _mimes.Add(".pdf", "application/pdf");
            _mimes.Add(".sql", "application/sql");
            _mimes.Add(".zip", "application/zip");
            _mimes.Add(".br", "application/brotli");
            _mimes.Add(".gz", "application/gzip");
            
            _mimes.Add(".bmp", "image/bmp");
            _mimes.Add(".png", "image/png");
            _mimes.Add(".jpg", "image/jpeg");
            _mimes.Add(".jpeg", "image/jpeg");
            _mimes.Add(".gif", "image/gif");
            _mimes.Add(".tif", "image/tiff");
            _mimes.Add(".tiff", "image/tiff");
            _mimes.Add(".webp", "image/webp");
            _mimes.Add(".jp2", "image/jp2");
            _mimes.Add(".avif", "image/avif");
            _mimes.Add(".apng", "image/apng");
            _mimes.Add(".heif", "image/heif");
            _mimes.Add(".heic", "image/heic");
            _mimes.Add(".jpx", "image/jpx");
            _mimes.Add(".jxr", "image/jxr");
            
            _mimes.Add(".wav", "audio/wav");
            _mimes.Add(".vorbis", "audio/vorbis");
            _mimes.Add(".opus", "audio/opus");
            _mimes.Add(".mp3", "audio/mp3");
            _mimes.Add(".mpa", "audio/mpa");
            _mimes.Add(".mka", "audio/mka");
            _mimes.Add(".ac3", "audio/ac3");
            _mimes.Add(".aac", "audio/aac");
            
            _mimes.Add(".avi", "video/avi");
            _mimes.Add(".av1", "video/av1");
            _mimes.Add(".mpv", "video/mpv");
            _mimes.Add(".mp4", "video/mp4");
            _mimes.Add(".mkv", "video/mkv");
            
            _mimes.Add(".ttf", "font/ttf");
            _mimes.Add(".otf", "font/otf");
            _mimes.Add(".woff", "font/woff");
            _mimes.Add(".woff2", "font/woff2");
        }
    }

    public string FindMimeType(string filename)
    {
        var ext = Path.GetExtension(filename);
        return _mimes.TryGetValue(ext, out var result)
            ? result
            : "application/octet-stream";
    }
}