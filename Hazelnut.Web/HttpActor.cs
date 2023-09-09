using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Hazelnut.Web.Configurations;
using Hazelnut.Web.Diagnostics;
using Hazelnut.Web.Handler;
using Hazelnut.Web.IO;

namespace Hazelnut.Web;

internal sealed class HttpActor : IDisposable
{
    private readonly Socket _acceptedSocket;
    private readonly HttpConnection _httpConnection;
    private readonly CancellationToken _cancellationToken;

    private readonly WeakReference<HttpServer> _parentServer;

    private readonly string _serverName;
    
    public HttpActor(HttpServer parentServer, Socket acceptedSocket, in ServerConfiguration serverConfig, CancellationToken cancellationToken)
    {
        _parentServer = new WeakReference<HttpServer>(parentServer);
        
        _acceptedSocket = acceptedSocket;
        _cancellationToken = cancellationToken;

        var localEndPoint = _acceptedSocket.LocalEndPoint;
        var remoteEndPoint = _acceptedSocket.RemoteEndPoint;

        Stream networkStream = new NetworkStream(_acceptedSocket);
        if (serverConfig.UseTls)
        {
            var sslStream = new SslStream(networkStream);
            sslStream.AuthenticateAsServer(serverConfig.TlsCertificate!);

            networkStream = sslStream;
            
            parentServer.Logger?.Write(LogLevel.Info, $"TLS connected from {remoteEndPoint}");
        }
        else
        {
            parentServer.Logger?.Write(LogLevel.Info, $"Plain connected from {remoteEndPoint}");
        }
        
        _httpConnection = new HttpConnection(networkStream, localEndPoint, remoteEndPoint);

        _serverName = serverConfig.ServerName;
    }
    
    ~HttpActor()
    {
        Dispose(false);
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        _httpConnection.Dispose();
        _acceptedSocket.Dispose();
    }

    public async ValueTask RunAsync()
    {
        while (_acceptedSocket.Connected)
        {
            if (!_parentServer.TryGetTarget(out var parentServer))
                break;
            
            try
            {
                if (!await InternalRunAsync(parentServer))
                    break;
            }
            catch (SocketException ex)
            {
                parentServer.Logger?.Write(LogLevel.Error, $"Connection closed by client from {_httpConnection.RemoteEndPoint}");
            }
        }
    }

    private async ValueTask<bool> InternalRunAsync(HttpServer parentServer)
    {
        var request = await Request.ParseRequestAsync(_httpConnection, parentServer, _cancellationToken);
        var response = new Response(request, _httpConnection, _serverName);

        var hostConfig =
            parentServer.HostConfigurations.TryGetValue(request.Headers.Host, out var getHostConfig)
                ? getHostConfig
                : parentServer.HostConfigurations[string.Empty];

        var requestHandler = hostConfig.FindRequestHandler(request.QueryString);
        if (requestHandler == null)
            throw new NullReferenceException();

        parentServer.Logger?.Write(LogLevel.Info,
            $"{request.Method.Method.ToUpper()} {request.QueryString} for {requestHandler} from {_httpConnection.RemoteEndPoint}");

        if (requestHandler.Location != "/")
            request.QueryString = request.QueryString[requestHandler.Location.Length..];

        if (requestHandler.AuthorizeMethod == null || requestHandler.AuthorizeMethod.IsAuthorized(request, response))
        {
            try
            {
                await requestHandler.OnRequestAsync(request, response);
            }
            catch (SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                parentServer.Logger?.Write(LogLevel.Error, ex.Message);
                response.StatusCode = HttpStatusCode.InternalServerError;
            }

            if (response is { StatusCode: HttpStatusCode.OK, IsStreamOpened: false })
            {
                // Empty Body
                await using var responseStream = response.OpenStream();
            }
        }

        if (!response.IsStreamOpened)
        {
            var errorPage = hostConfig.ErrorPageProvider.Provide(response.StatusCode);
            await using var responseStream = response.OpenStream();
            await errorPage.CopyToAsync(responseStream, _cancellationToken);
        }

        if (response.HttpVersion == new Version(1, 0) ||
            response.Headers["Connection"]?.Equals("Close", StringComparison.OrdinalIgnoreCase) == true)
        {
            parentServer.Logger?.Write(LogLevel.Info, $"Connection closed from {_httpConnection.RemoteEndPoint}");
            _acceptedSocket.Close();
            return false;
        }

        return true;
    }
}