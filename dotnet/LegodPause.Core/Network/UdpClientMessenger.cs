using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LegodPause.Core.Network;

public class UdpClientMessenger(ILogger<UdpClientMessenger> logger) : IPipeReceiveHandle, IPipSendHandle
{
    private UdpClient udpClient;

    public async ValueTask<int> ReceiveBytesHandle(Memory<byte> buffer, CancellationToken token)
    {
        var udpReceiveResult = await udpClient.ReceiveAsync(
#if NETCOREAPP
            token
#endif
        );
        udpReceiveResult.Buffer.CopyTo(buffer);
        // remoteEndPoint = udpReceiveResult.RemoteEndPoint;
        return udpReceiveResult.Buffer.Length;
    }

    public Task SendBytesHandle(ArraySegment<byte> data, CancellationToken token)
    {
        return udpClient.SendAsync(data.ToArray(), data.Count);
    }

    public void RegisterSender(UdpClient udpClient, PipeChannel brodcastPipe)
    {
        this.udpClient = udpClient;
        brodcastPipe.AddSend(this);
    }
}