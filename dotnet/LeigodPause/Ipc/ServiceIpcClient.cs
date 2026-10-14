using System.IO.Pipes;
using System.Threading;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace LeigodPause.Ipc;

public sealed class ServiceIpcClient(ILogger<ServiceIpcClient> logger, LeigodCoreModel leigodCoreModel, ProtobufMsgEncoder protobufMsgEncoder) : IDisposable
{
    private NamedPipeClientStream _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);

    // 保护共享的 _pipe：Connect 与 SendAsync 互斥，避免并发读写导致“操作正在进行中/管道已断开”。
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsConnected => _pipe.IsConnected;


    public async Task<IpcEnvelope?> SendAsync(IpcEnvelope command, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!_pipe.IsConnected)
            {
                logger.LogInformation("Pipe is not connected");
                return null;
            }

            try
            {
                // await EnsureConnectedAsync(token).ConfigureAwait(false);
                await protobufMsgEncoder.Encode(_pipe!, command, token).ConfigureAwait(false);
                var result = await protobufMsgEncoder.Decode<IpcEnvelope>(_pipe!, token).ConfigureAwait(false);
                return result;
            }
            catch (IOException ioEx)
            {
                // 管道已断开（服务重启/崩溃）：重建实例，使 Connect() 下一轮能重连；
                // 本次请求返回 null 让调用方知道失败。已在 _gate 内，无需重复获取锁。
                logger.LogWarning(ioEx, "IPC 读写失败，重建管道以便重连");
                _pipe.Dispose();
                _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _gate?.Dispose();
    }

    public async Task Connect()
    {
        while (true)
        {
            // 1) 仅在（重）连接时短暂持锁，连上后立即释放——否则会在整个连接期内一直
            //    持有 _gate，导致 SendAsync 永远拿不到锁而被饿死（死锁）。
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_pipe.IsConnected)
                {
                    // Timeout avoids blocking the UI thread if the service is not running.
                    try
                    {
                        await _pipe.ConnectAsync(3000).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // 服务未启动/超时：稍后重试
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "ConnectAsync failed");
                    }
                }
            }
            finally
            {
                _gate.Release();
            }

            // 2) 已连接：不持锁地等待，直到管道断开，让 SendAsync 能正常收发。
            //    注意：服务器崩溃（未正常关闭管道）时 IsConnected 不会立刻变 false，
            //    此时由 SendAsync 的读写异常来兜底（异常后 IsConnected 会翻转，
            //    下次进入步骤 3 重建）。
            try
            {
                while (_pipe.IsConnected)
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // 连接断开 / 被释放时退出等待，进入重建流程
            }

            // 3) 已断开：持锁重建管道实例，再回到循环重连。
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _pipe.Dispose();
                _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}