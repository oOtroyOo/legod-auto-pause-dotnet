using System.Net;

namespace LegodPause.Core.Network;

public interface IPipeReceiveHandle
{
    ValueTask<int> ReceiveBytesHandle(Memory<byte> buffer, CancellationToken token);
}