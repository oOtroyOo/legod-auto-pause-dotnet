using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LeigodPause.Service.Ipc;

public sealed class ServiceIpcServer(LeigodCoreModel leigodCoreModel, ILogger<ServiceIpcServer> logger, ProtobufMsgEncoder protobufMsgEncoder)
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;

    // 当前正在服务的客户端连接；服务端主动下发时使用它。
    private volatile NamedPipeServerStream? _currentPipe;

    // 串行化所有对管道的写（响应 + 主动下发），避免并发写导致帧交错。
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    // 上一次下发的游戏进程结果，用于去重（未变化不重复下发）。
    private string? _lastPushedGameMsg;

    public void Start()
    {
        logger.LogInformation("IPC pipe server starting on {PipeName}", IpcPipeNames.CommandPipe);
        _listenTask = ListenLoopAsync(_cts.Token);
    }

    public Task Stop()
    {
        logger.LogInformation("IPC pipe server stopping");
        _cts.Cancel();
        return _listenTask ?? Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SecurityIdentifier securityIdentifier = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            PipeSecurity pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(securityIdentifier, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
            NamedPipeServerStream pipe = new NamedPipeServerStream(IpcPipeNames.CommandPipe, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 0, 0, pipeSecurity);

            try
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception e)
            {
                pipe.Dispose();
                logger.LogError(e, "Named pipe wait-connection failed");
                continue;
            }

            logger.LogInformation("IPC client connected on {PipeName}", IpcPipeNames.CommandPipe);
            await HandleClientAsync(pipe, token);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        _currentPipe = pipe;
        try
        {
            // 保持连接：一条连接循环处理多条指令，直到客户端断开或服务停止，
            while (!token.IsCancellationRequested)
            {
                IpcEnvelope command;
                try
                {
                    command = await protobufMsgEncoder.Decode<IpcEnvelope>(pipe, token).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    // 客户端断开：读取到 0 字节
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "IPC 读取失败，关闭该连接");
                    break;
                }

                logger.LogInformation("IPC command received: {Command}", command.Command);

                var result = await DispatchAsync(command).ConfigureAwait(false);

                // 响应与主动下发共用写锁，避免两处并发写导致帧交错
                await _writeGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await protobufMsgEncoder.Encode(pipe, result, token).ConfigureAwait(false);
                }
                finally
                {
                    _writeGate.Release();
                }
            }
        }
        finally
        {
            if (ReferenceEquals(_currentPipe, pipe))
            {
                _currentPipe = null;
            }

            pipe.Dispose();
            logger.LogDebug("IPC client disconnected");
        }
    }

    private async Task<IpcEnvelope> DispatchAsync(IpcEnvelope cmd)
    {
        try
        {
            var (code, msg, data) = cmd.Command switch
            {
                ServiceCommand.Ping => (0, null, (byte[]?)null),
                ServiceCommand.RefreshUserInfo => await DoRefreshAsync(),
                ServiceCommand.PauseState => await DoPauseAsync(),
                ServiceCommand.ReloadConfig => await DoReloadConfig(),
                ServiceCommand.FindGameProcess => await DoFindGameProcess(),
                ServiceCommand.Exit => (0, (string?)null, (byte[]?)null),
                _ => (-2, "Unknown command", null),
            };
            return new IpcEnvelope { Command = cmd.Command, Code = code, Msg = msg, Payload = data };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Command handling failed");
            return new IpcEnvelope { Command = cmd.Command, Code = -1, Msg = e.GetType().Name + ":" + e.Message };
        }
    }


    private async Task<(int Code, string? Msg, byte[]? Data)> DoReloadConfig()
    {
        ConfigFile.Reload();
        return (0, null, null);
    }

    public IpcEnvelope? LastUserInfoError { get; set; }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoRefreshAsync()
    {
        int? errCode = null;
        string? errMsg = null;

        bool needRefresh = LastUserInfoError == null && leigodCoreModel.UserInfoJson == null || leigodCoreModel.RequestInfoTimeout;
        if (needRefresh)
        {
            await leigodCoreModel.RequestUserInfo((c, m) =>
            {
                errCode = c;
                errMsg = m;
                LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
                return Task.CompletedTask;
            });
        }

        // 本次刷新返回明确错误：直接上报错误（保留 LastUserInfoError 不变）
        if (errCode is { } code && code != 0)
        {
            return (code, errMsg, null);
        }

        // 已有有效用户信息：清除历史错误，避免误报“登录失效”并清空 Token
        if (leigodCoreModel.UserInfoJson != null)
        {
            LastUserInfoError = null;
            var json = JsonSerializer.Serialize(leigodCoreModel.UserInfoJson, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });
            return (0, null, Encoding.UTF8.GetBytes(json));
        }

        // 既无新错误也无缓存：回退到历史错误（若有）
        if (LastUserInfoError != null)
        {
            return (LastUserInfoError.Code, LastUserInfoError.Msg, null);
        }

        return (0, null, null);
    }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoPauseAsync()
    {
        int? errCode = null;
        string? errMsg = null;
        var executed = await leigodCoreModel.SendPauseState((c, m) =>
        {
            errCode = c;
            errMsg = m;
            return Task.CompletedTask;
        });

        // 在途跳过：明确告知客户端“请求进行中”，而非伪装成功
        if (!executed)
        {
            return (-100, "请求进行中，已跳过", null);
        }

        if (errCode is { } code && code != 0)
        {
            return (code, errMsg, null);
        }

        return (0, null, null);
    }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoFindGameProcess()
    {
        var gameMsg = await leigodCoreModel.FindGameProcess().ConfigureAwait(false);
        if (string.IsNullOrEmpty(gameMsg))
        {
            return (0, null, null);
        }

        return (0, null, Encoding.UTF8.GetBytes(gameMsg));
    }

    /// <summary>
    /// 主动向已连接的客户端下发游戏进程检测结果（服务端发起，不要求回执）。
    /// 结果未变化时不重复下发；失败仅记日志，允许数据丢弃。
    /// </summary>
    public async Task PushFindGameProcessAsync(string? gameMsg)
    {
        if (string.Equals(gameMsg, _lastPushedGameMsg, StringComparison.Ordinal))
        {
            return;
        }

        var pipe = _currentPipe;
        if (pipe is null || !pipe.IsConnected)
        {
            // 无客户端连接：更新去重基准即可，本次数据丢弃
            _lastPushedGameMsg = gameMsg;
            return;
        }

        var envelope = new IpcEnvelope
        {
            Command = ServiceCommand.FindGameProcess,
            Code = 0,
            Payload = string.IsNullOrEmpty(gameMsg) ? null : Encoding.UTF8.GetBytes(gameMsg),
        };

        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await protobufMsgEncoder.Encode(pipe, envelope, CancellationToken.None).ConfigureAwait(false);
            _lastPushedGameMsg = gameMsg;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "下发游戏进程结果失败（允许丢弃）");
        }
        finally
        {
            _writeGate.Release();
        }
    }
}