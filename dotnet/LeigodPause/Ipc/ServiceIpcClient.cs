using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LeigodPause.Service.Ipc;
using LeigodPause.Service.Models;
using LeigodPause.Service.Network;
using Microsoft.Extensions.Logging;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace LeigodPause.Ipc;

public sealed class ServiceIpcClient(ILogger<ServiceIpcClient> logger, LeigodCoreModel leigodCoreModel, ProtobufMsgEncoder protobufMsgEncoder) : IDisposable
{
    private NamedPipeClientStream _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);


    public async Task<IpcEnvelope> SendAsync(IpcEnvelope command, CancellationToken token = default)
    {
        if (!_pipe.IsConnected)
        {
            throw new InvalidOperationException("Pipe is not connected");
        }

        // await EnsureConnectedAsync(token).ConfigureAwait(false);
        await protobufMsgEncoder.Encode(_pipe!, command, token).ConfigureAwait(false);
        var result = await protobufMsgEncoder.Decode<IpcEnvelope>(_pipe!, token).ConfigureAwait(false);
        return result;
    }

    public void Dispose() => _pipe?.Dispose();

    public async Task Connect()
    {
        while (true)
        {
            if (!_pipe.IsConnected)
            {
                // Timeout avoids blocking the UI thread if the service is not running.
                try
                {
                    await _pipe.ConnectAsync(3000).ConfigureAwait(false);
                }
                catch (TimeoutException e)
                {
                    //
                }
                catch (Exception e)
                {
                    logger.LogError(e, "ConnectAsync failed");
                }
            }

            await Task.Delay(3000);
        }
    }
}