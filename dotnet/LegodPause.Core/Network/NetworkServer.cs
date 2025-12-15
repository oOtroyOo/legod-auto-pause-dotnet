using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LegodPause.Core.Proto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace LegodPause.Core.Network;

public class NetworkServer : IDisposable
{
    public static int TcpPort = 8555;
    public static int UdpPort = 8566;
    TcpListener _tcpListener;

    private PipeChannel? brodcastPipe = null;
    List<PipeChannel> tcpClients = new List<PipeChannel>();
    private readonly ILogger<NetworkServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CancellationToken _cancellationToken;
    System.Threading.Timer? heartbeatTimer;
    private long _seq = 0;

    ProtobufMsgEncoder _encoder;

    public NetworkServer(ILogger<NetworkServer> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _encoder = new ProtobufMsgEncoder(serviceProvider.GetService<ILogger<ProtobufMsgEncoder>>());
    }

    public void Start(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _cancellationToken.Register(Dispose);
        InitBroadcast();
        InitListener();
        InitHeart();
    }

    private void InitHeart()
    {
        heartbeatTimer = new System.Threading.Timer(_ =>
        {
            _logger.LogInformation("Heartbeat");
            Bradcast(new ProtoLib()
            {
                Ping = new ProtoPing() { pingTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() }
            }).Wait(_cancellationToken);
        }, null, 5000, 5000);
        _cancellationToken.Register(() => { heartbeatTimer?.Dispose(); });
    }

    private void InitListener()
    {
        _tcpListener = new TcpListener(IPAddress.Any, TcpPort);
        _cancellationToken.Register(() => { _tcpListener?.Stop(); });
        Task.Run(async () =>
        {
            _tcpListener.Start();
            while (!_cancellationToken.IsCancellationRequested)
            {
                var client = await _tcpListener.AcceptTcpClientAsync();
                PipeChannel pipe = new PipeChannel(_cancellationToken, _serviceProvider.GetService<ILogger<PipeChannel>>());
                tcpClients.Add(pipe);
                pipe.AddSocket(client.Client);
            }
        }, _cancellationToken);
    }

    private void InitBroadcast()
    {
        if (brodcastPipe == null)
        {
            brodcastPipe = new PipeChannel(_cancellationToken, _serviceProvider.GetService<ILogger<PipeChannel>>());
            _cancellationToken.Register(() => { brodcastPipe.Dispose(); });
        }

        foreach (var address in NetworkUtil.GetAllIPBradcast())
        {
            var endPoint = new IPEndPoint(address, UdpPort);
            var udpClient = new UdpClient(address.AddressFamily);
            try
            {
                udpClient.Connect(endPoint);
                brodcastPipe.AddSocket(udpClient.Client);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "udpClient.Connect error");
            }
        }

        // Bradcast(new ProtoLib()
        // {
        //     ServerInfo = new ProtoServerInfo()
        //     {
        //         IpAddress = NetworkUtil.GetAllIP().Select(x => x.ToString()).ToArray(),
        //         TcpPort = TcpPort,
        //         UdpPort = UdpPort
        //     }
        // }).Wait(_cancellationToken);
    }

    public async Task Bradcast<T>(T pack) where T : ProtoLib
    {
        try
        {
            // InitBroadcast();
            heartbeatTimer?.Change(5000, 5000);
            Interlocked.Increment(ref _seq);
            pack.Seq = _seq;


            var length = await _encoder.Encode(brodcastPipe.Send.Writer, pack);
            _logger.LogInformation("Encode Bradcast {length} bytes", length);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Bradcast error");
        }
    }

    private void Stop()
    {
        foreach (var pipe in tcpClients)
        {
            try
            {
                pipe.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "pipe.Dispose error");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}