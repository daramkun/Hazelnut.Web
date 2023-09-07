using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Hazelnut.Web.Configurations;
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

    public async Task RunAsync()
    {
        while (_acceptedSocket.Connected)
        {
            if (!_parentServer.TryGetTarget(out var parentServer))
                break;

            var request = await Request.ParseRequestAsync(_httpConnection, parentServer, _cancellationToken);
            var response = new Response(request, _httpConnection, _serverName);

            var hostConfig =
                parentServer.HostConfigurations.TryGetValue(request.Headers.Host, out var getHostConfig)
                    ? getHostConfig
                    : parentServer.HostConfigurations[string.Empty];
            
            var requestHandler = hostConfig.FindRequestHandler(request.QueryString);
            if (requestHandler == null)
                throw new NullReferenceException();
            
            if (requestHandler.AuthorizeMethod == null || requestHandler.AuthorizeMethod.IsAuthorized(request, response))
            {
                try
                {
                    await requestHandler.OnRequestAsync(request, response);
                }
                catch (Exception ex)
                {
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
                _acceptedSocket.Close();
                break;
            }
        }
    }
}