using System.Net;
using System.Net.Sockets;

namespace LegodPause.Core.Network;

public class TcpClientMessenger(TcpClient tcpClient, CancellationToken cancellationToken) : IPipeReceiveHandle, IPipSendHandle, IDisposable
{
    public ValueTask<int> ReceiveBytesHandle(Memory<byte> buffer, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task SendBytesHandle(ArraySegment<byte> data, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        tcpClient.Dispose();
    }
}