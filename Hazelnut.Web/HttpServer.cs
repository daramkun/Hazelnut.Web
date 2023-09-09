using System.Net;
using System.Net.Sockets;
using Hazelnut.Web.Configurations;
using Hazelnut.Web.Diagnostics;

namespace Hazelnut.Web;

public class HttpServer : IDisposable, IAsyncDisposable
{
    private readonly Socket[] _listenSockets;
    private readonly Dictionary<Socket, ServerConfiguration> _serverConfigs = new();
    private readonly Dictionary<string, HostConfiguration> _hostConfigs = new();
    private readonly CancellationTokenSource _cancellationToken;

    public event EventHandler? Ready;
    public event EventHandler? ShuttingDown;

    public bool IsAlive => !_cancellationToken.IsCancellationRequested;

    public IReadOnlyCollection<ServerConfiguration> ServerConfigurations => _serverConfigs.Values;
    public IReadOnlyDictionary<string, HostConfiguration> HostConfigurations => _hostConfigs;
    
    public ulong MaximumPostContentLength { get; }

    public IWebLogger? Logger { get; set; } = new DefaultWebLogger();

    public HttpServer(ServerConfiguration[] serverConfigs, HostConfiguration[] hostConfigs, ulong maximumPostContentLength = ulong.MaxValue)
    {
        if (serverConfigs.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(serverConfigs));
        if (hostConfigs.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(hostConfigs));

        if (serverConfigs.Select(config => config.Port).Distinct().Count() != serverConfigs.Length)
            throw new ArgumentException("Server port number is duplicated.");

        var listenSockets = new List<Socket>();
        foreach (var serverConfig in serverConfigs)
        {
            if (serverConfig is { UseTls: true, TlsCertificate: null })
                throw new ArgumentException("TLS option is enabled but certificate is null.");

            var listenSocket = new Socket(serverConfig.BindAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(serverConfig.BindAddress, serverConfig.Port));

            listenSockets.Add(listenSocket);
            _serverConfigs.Add(listenSocket, serverConfig);
        }
        _listenSockets = listenSockets.ToArray();
        
        foreach (var hostConfig in hostConfigs)
        {
            if (serverConfigs.All(config => config.Port != hostConfig.TargetPort))
                throw new ArgumentOutOfRangeException(nameof(hostConfigs));
            
            var targetHosts = hostConfig.TargetHosts;
            if (hostConfig.TargetHosts.Length == 0)
                targetHosts = new[] { string.Empty };

            if (targetHosts.Any(targetHost => _hostConfigs.ContainsKey(targetHost)))
                throw new ArgumentException($"Target Host is exist: {string.Join(',', targetHosts)}");
            
            foreach (var targetHost in targetHosts)
                _hostConfigs.Add(targetHost, hostConfig);
        }

        _cancellationToken = new CancellationTokenSource();

        MaximumPostContentLength = maximumPostContentLength;
    }

    public void Dispose()
    {
        _cancellationToken.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        _cancellationToken.Dispose();
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    public void Run(int backlog = 200)
    {
        foreach (var listenSocket in _listenSockets)
            listenSocket.Listen(backlog);
        
        Logger?.Write(LogLevel.Info, "Server is ready.");
        OnReady();

        Task.WaitAll(
            _listenSockets.Select(listenSocket => AcceptConnection(listenSocket, _cancellationToken.Token)).ToArray(),
            _cancellationToken.Token
        );

        Logger?.Write(LogLevel.Info, "Server is shutting down.");
        OnShuttingDown();
    }

    public void Shutdown()
    {
        if (!_cancellationToken.IsCancellationRequested)
            _cancellationToken.Cancel();
    }

    protected virtual void OnReady()
    {
        Ready?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnShuttingDown()
    {
        ShuttingDown?.Invoke(this, EventArgs.Empty);
    }

    private async Task AcceptConnection(Socket listenSocket, CancellationToken cancellationToken)
    {
        var queuedTasks = Array.Empty<Task>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var acceptedSocket = await listenSocket.AcceptAsync(cancellationToken);
            queuedTasks = queuedTasks
                    .Where(task => task is { IsCompleted: false, IsCanceled: false, IsFaulted: false, IsCompletedSuccessfully: false })
                    .Append(QueueActor(listenSocket, acceptedSocket, cancellationToken))
                    .ToArray();
        }

        // cancellationToken is not for Task.WaitAll()
#pragma warning disable CA2016
        Task.WaitAll(queuedTasks);
#pragma warning restore CA2016
    }

    private async Task QueueActor(Socket listenSocket, Socket acceptedSocket, CancellationToken cancellationToken)
    {
        using var actor = new HttpActor(this, acceptedSocket, _serverConfigs[listenSocket], cancellationToken);
        await actor.RunAsync();
    }
}