using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using LegodPause.Core.Proto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;
using TouchSocket.Core;
using TouchSocket.Sockets;
using Result = TouchSocket.Core.Result;

namespace LegodPause.Core.Network;

public class NetworkServer(ILogger<NetworkServer> logger, IServiceProvider serviceProvider, IConfigurationRoot localConfig) : ServiceBase, IDisposable
{
    public static int TcpPort = 8555;
    public static int UdpPort = 8566;
    private UdpSession udpService;
    private TcpService tcpService;
    public override ServerState ServerState => tcpService.ServerState;

    private IConfigurationSection? localConfigSection = localConfig?.GetSection("config");
    ProtoPackageAdapter packageAdapter = new ProtoPackageAdapter();

    protected override void LoadConfig(TouchSocketConfig config)
    {
        config
            // .SetBindIPHost(UdpPort)
            .SetListenIPHosts(TcpPort)
            .SetTcpDataHandlingAdapter(() => packageAdapter)
            .SetUdpDataHandlingAdapter(() => new UdpProtoPackageAdapter(packageAdapter))
            .ConfigureContainer(a => // 
            {
            })
            .ConfigurePlugins(a => // 
            {
                a.AddLegodPusePlugin();
                a.AddTcpConnectedPlugin(TcpConnectedHandle);
                //a.UseTcpSessionCheckClear(options =>
                // {
                //     options.
                //     options.CheckClearType = CheckClearType.All;
                //     options.Tick = TimeSpan.FromSeconds(60);
                //     options.OnClose = async (c, t) =>
                //     {
                //         await c.CloseAsync("超时无数据");
                //     };
                // });
                a.AddTcpSendingPlugin(TcpSendHandleTask);
            });
        base.LoadConfig(config);
    }


    private async Task TcpConnectedHandle(ITcpSession arg1, ConnectedEventArgs arg2)
    {
        await arg2.InvokeNext();
    }


    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var value = localConfigSection?.GetValue<string>("path");
        udpService = new UdpSession();
        tcpService = new TcpService();
        await udpService.SetupAsync(base.Config);
        udpService.Config.RemoteIPHost = new IPHost(IPAddress.Broadcast, UdpPort);
        await udpService.StartAsync(cancellationToken);
        await tcpService.SetupAsync(base.Config);
        await tcpService.StartAsync(cancellationToken);
        tcpService.Received += Received;
        await Task.Run(() => ExecuteAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private Task Received(TcpSessionClient client, ReceivedDataEventArgs e)
    {
        if (e.RequestInfo is ProtoLib myRequest)
        {
            client.Logger.Info($"已从{client.Id}接收到,seq={myRequest.Seq}");
        }

        return e.InvokeNext();
    }

    public override async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Result.Success;
        }
        catch (TaskCanceledException e)
        {
            return Result.Canceled;
        }
        catch (Exception e)
        {
            logger.LogError(e, "StopAsync error");
            return Result.FromException(e);
        }
    }


    private Task TcpSendHandleTask(ITcpSession arg1, SendingEventArgs arg2)
    {
        return arg2.InvokeNext();
    }

    protected async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var ts = now.ToUnixTimeSeconds();
            logger.LogInformation("Worker running at:{ts} {time}", ts, now);
            var pack = new ProtoLib() { Ping = new() { pingTime = ts } };
            await Broadcast(pack, stoppingToken);

            await Task.Delay(5000, stoppingToken);
        }
    }

    public async Task Broadcast<T>(T pack, CancellationToken stoppingToken) where T : ProtoLib
    {
        var readOnlyMemory = pack.BuildAsBytes();
        StringBuilder log = new();
        StringBuilder error = new();
        foreach (var address in NetworkUtil.GetAllIPBradcast())
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6) continue;
            var endPoint = new IPEndPoint(address, UdpPort);
            try
            {
                await this.udpService.SendAsync(endPoint, readOnlyMemory, stoppingToken);
                log.AppendLine(endPoint.ToString());
            }
            catch (Exception e)
            {
                error.AppendLine(endPoint.ToString());
            }
        }

        if (log.Length > 0)
        {
            logger.LogInformation("Broadcast success seq={seq} > {log}", pack.Seq, log);
        }

        if (error.Length > 0)
        {
            logger.LogError("Broadcast error seq={seq} > {log}", pack.Seq, log);
        }
    }

    private void Stop()
    {
        udpService?.Dispose();
        tcpService?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}