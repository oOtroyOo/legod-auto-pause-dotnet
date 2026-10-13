using System.IO.Pipes;
using System.Text.Json.Nodes;
using LeigodPause.Service.Models;
using Microsoft.Extensions.Logging;

namespace LeigodPause.Service.Ipc;

public sealed class ServiceIpcServer
{
    private readonly ILogger<ServiceIpcServer>? _logger;
    private readonly LeigodCoreModel _model;
    private readonly CancellationTokenSource _cts = new();

    public ServiceIpcServer(LeigodCoreModel model, ILogger<ServiceIpcServer>? logger = null)
    {
        _model = model;
        _logger = logger;
    }

    public void Start() => _ = ListenLoopAsync(_cts.Token);

    public void Stop() => _cts.Cancel();

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                IpcPipeNames.CommandPipe,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

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
                _logger?.LogError(e, "Named pipe wait-connection failed");
                continue;
            }

            _ = HandleClientAsync(pipe, token);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        try
        {
            var command = await IpcTransport.ReadEnvelopeAsync(pipe, token).ConfigureAwait(false);
            var result = await DispatchAsync(command).ConfigureAwait(false);
            await IpcTransport.WriteEnvelopeAsync(pipe, result, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Named pipe command handling failed");
        }
        finally
        {
            pipe.Dispose();
        }
    }

    private async Task<IpcEnvelope> DispatchAsync(IpcEnvelope cmd)
    {
        var (ok, code, msg, data) = cmd.Command switch
        {
            ServiceCommand.RefreshUserInfo => await DoRefreshAsync(),
            ServiceCommand.StartPause => await DoPauseAsync(),
            ServiceCommand.StopPause => (false, -2, "Not implemented: resume pause", (JsonNode?)null),
            ServiceCommand.Exit => (true, 0, (string?)null, (JsonNode?)null),
            _ => (false, -2, "Unknown command", null),
        };

        var payload = new JsonObject();
        payload["ok"] = JsonValue.Create(ok);
        if (code != 0)
        {
            payload["code"] = JsonValue.Create(code);
        }

        if (msg != null)
        {
            payload["msg"] = JsonValue.Create(msg);
        }

        if (data != null)
        {
            payload["data"] = data;
        }

        return new IpcEnvelope { Command = cmd.Command, Payload = payload.ToJsonString() };
    }

    private async Task<(bool Ok, int Code, string? Msg, JsonNode? Data)> DoRefreshAsync()
    {
        int? errCode = null;
        string? errMsg = null;
        await _model.RequestUserInfo((c, m) => { errCode = c; errMsg = m; });

        if (errCode is { } code && code != 0)
        {
            return (false, code, errMsg, null);
        }

        return (true, 0, null, _model.UserInfoJson?.DeepClone());
    }

    private async Task<(bool Ok, int Code, string? Msg, JsonNode? Data)> DoPauseAsync()
    {
        int? errCode = null;
        string? errMsg = null;
        await _model.SendPauseState((c, m) => { errCode = c; errMsg = m; });

        if (errCode is { } code && code != 0)
        {
            return (false, code, errMsg, null);
        }

        return (true, 0, null, null);
    }
}
