namespace LegodPause.Core.Network;

public interface IPipSendHandle
{
    Task SendBytesHandle(ArraySegment<byte> data, CancellationToken token);
}