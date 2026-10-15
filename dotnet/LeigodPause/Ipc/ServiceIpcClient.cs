using System.IO.Pipes;
using System.Text;
using System.Threading;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace LeigodPause.Ipc;

public sealed class ServiceIpcClient(ILogger<ServiceIpcClient> logger, LeigodCoreModel leigodCoreModel, ProtobufMsgEncoder protobufMsgEncoder) : IDisposable
{
    // 等待服务端响应的超时：超时按“数据丢弃”处理，避免连接异常时无限阻塞轮询
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    private NamedPipeClientStream _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);

    // 保护共享的 _pipe：Connect / SendAsync(写) / 重建 互斥。
    // 读统一交给接收泵负责，因此泵不参与此锁，读写互不阻塞。
    private readonly SemaphoreSlim _gate = new(1, 1);

    // 构造于 UI 线程，用于把服务端下发的结果切回 UI 线程写绑定属性
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    // 同一时刻最多一个在途请求（SendAsync 已被 _gate 串行化），响应由接收泵回填
    private TaskCompletionSource<IpcEnvelope>? _pending;
    private ServiceCommand _pendingCommand;
    private readonly object _pendingLock = new();

    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private readonly object _receiveLock = new();

    public bool IsConnected => _pipe.IsConnected;

    public async Task<IpcEnvelope?> SendAsync(IpcEnvelope command, CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                lock (_pendingLock)
                {
                    _pending = tcs;
                    _pendingCommand = command.Command;
                }

                // 只写：响应由 ReceiveLoopAsync 读取后回填 tcs
                await protobufMsgEncoder.Encode(_pipe, command, token).ConfigureAwait(false);
            }
            catch (IOException ioEx)
            {
                // 管道已断开（服务重启/崩溃）：重建实例让 Connect() 下一轮重连，本次丢弃。
                logger.LogWarning(ioEx, "IPC 写入失败，重建管道以便重连");
                ClearPending(tcs);
                RecreatePipe();
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }

        // 在锁外等待响应，避免阻塞其它调用与接收泵
        try
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(ReceiveTimeout, token)).ConfigureAwait(false);
            if (completed != tcs.Task)
            {
                ClearPending(tcs);
                if (!token.IsCancellationRequested)
                {
                    logger.LogWarning("等待 IPC 响应超时：{Command}", command.Command);
                }

                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ClearPending(tcs);
            return null;
        }
    }

    private void ClearPending(TaskCompletionSource<IpcEnvelope> tcs)
    {
        lock (_pendingLock)
        {
            if (ReferenceEquals(_pending, tcs))
            {
                _pending = null;
            }
        }
    }

    private void RecreatePipe()
    {
        try
        {
            _pipe.Dispose();
        }
        catch
        {
            // 忽略释放异常
        }

        _pipe = new NamedPipeClientStream(".", IpcPipeNames.CommandPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    private void EnsureReceiveLoop()
    {
        lock (_receiveLock)
        {
            if (_receiveTask != null)
            {
                return;
            }

            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }
    }

    /// <summary>
    /// 统一的读端：客户端所有帧都在这里读取。
    /// FindGameProcess 帧 = 服务端主动下发（无回执）；其余帧 = 当前在途请求的响应。
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 每轮取最新管道实例，便于断线重连后自动接管新管道
            var pipe = _pipe;
            if (pipe is null || !pipe.IsConnected)
            {
                await SafeDelay(200, token);
                continue;
            }

            try
            {
                var frame = await protobufMsgEncoder.Decode<IpcEnvelope>(pipe, token).ConfigureAwait(false);
                HandleIncomingFrame(frame);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (EndOfStreamException)
            {
                await SafeDelay(200, token);
            }
            catch (ObjectDisposedException)
            {
                await SafeDelay(200, token);
            }
            catch (IOException)
            {
                await SafeDelay(200, token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "IPC 接收失败");
                await SafeDelay(500, token);
            }
        }
    }

    private static async Task SafeDelay(int ms, CancellationToken token)
    {
        try
        {
            await Task.Delay(ms, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 退出等待
        }
    }

    private void HandleIncomingFrame(IpcEnvelope envelope)
    {
        // 服务端主动下发：不属于任何请求的响应，直接更新 UI，不发回执。
        if (envelope.Command == ServiceCommand.FindGameProcess)
        {
            var msg = envelope.Payload is { Length: > 0 } ? Encoding.UTF8.GetString(envelope.Payload) : string.Empty;
            ApplyGameProcessPush(msg);
            return;
        }

        if (envelope.Command == ServiceCommand.RefreshUserInfo)
        {
            if (envelope.Code != 0)
            {
                leigodCoreModel.Token = null;
                var messageBox = new MessageBox
                {
                    Content = $"{envelope.Code}\n{envelope.Msg}",
                    Title = "用户登录失效",
                    CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                };
                messageBox.ShowDialogAsync();
            }
        }

        if (envelope.Command == ServiceCommand.PauseState)
        {
            if (envelope.Code != 0)
            {
                var messageBox = new MessageBox
                {
                    Content = $"{envelope.Code}\n{envelope.Msg}",
                    Title = "暂停调用失败",
                    CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                };
                messageBox.ShowDialogAsync();
            }
        }

        // 其余帧：回填给当前在途请求。校验 Command 匹配，避免超时迟到的响应串到下一个请求。
        TaskCompletionSource<IpcEnvelope>? pending = null;
        lock (_pendingLock)
        {
            if (_pending != null && _pendingCommand == envelope.Command)
            {
                pending = _pending;
                _pending = null;
            }
        }

        pending?.TrySetResult(envelope);
    }

    private void ApplyGameProcessPush(string msg)
    {
        if (_uiContext != null)
        {
            _uiContext.Post(_ => leigodCoreModel.MatchedGameProcess = msg, null);
        }
        else
        {
            leigodCoreModel.MatchedGameProcess = msg;
        }
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _pipe?.Dispose();
        _gate?.Dispose();
    }

    public async Task Connect()
    {
        EnsureReceiveLoop();

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
                RecreatePipe();
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}