using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LeigodPause.Service.Ipc;

namespace LeigodPause.Ipc;

public sealed class ServiceIpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;

    public async Task<IpcEnvelope> SendAsync(IpcEnvelope command, CancellationToken token = default)
    {
        await EnsureConnectedAsync(token).ConfigureAwait(false);
        await IpcTransport.WriteEnvelopeAsync(_pipe!, command, token).ConfigureAwait(false);
        return await IpcTransport.ReadEnvelopeAsync(_pipe!, token).ConfigureAwait(false);
    }

    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        _pipe ??= new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        if (!_pipe.IsConnected)
        {
            // Timeout avoids blocking the UI thread if the service is not running.
            await _pipe.ConnectAsync(3000).ConfigureAwait(false);
        }
    }

    public void Dispose() => _pipe?.Dispose();
}
