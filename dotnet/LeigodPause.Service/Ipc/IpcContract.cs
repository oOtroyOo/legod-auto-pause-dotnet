using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace LeigodPause.Service.Ipc;

public static class IpcPipeNames
{
    // Frontend -> Service: send commands
    public const string CommandPipe = "LeigodPauseSvcCommands";
    // Service -> Frontend: push status (optional)
    public const string StatusPipe = "LeigodPauseSvcStatus";
}

public enum ServiceCommand
{
    None = 0,
    StartPause,
    StopPause,
    RefreshUserInfo,
    Exit,
}

public class IpcEnvelope
{
    public ServiceCommand Command { get; set; }
    public string? Payload { get; set; }
}

// Shared pipe transport: 4-byte length prefix (little-endian) + UTF-8 JSON body.
public static class IpcTransport
{
    public static async Task<IpcEnvelope> ReadEnvelopeAsync(PipeStream pipe, CancellationToken token)
    {
        var lenBuf = await ReadExactAsync(pipe, 4, token).ConfigureAwait(false);
        var len = BitConverter.ToInt32(lenBuf, 0);
        var body = await ReadExactAsync(pipe, len, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<IpcEnvelope>(body)!;
    }

    public static async Task WriteEnvelopeAsync(PipeStream pipe, IpcEnvelope envelope, CancellationToken token)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var len = BitConverter.GetBytes(body.Length);
        await pipe.WriteAsync(len, 0, len.Length, token).ConfigureAwait(false);
        await pipe.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
        await pipe.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(PipeStream pipe, int count, CancellationToken token)
    {
        var buf = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await pipe.ReadAsync(buf, read, count - read, token).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }

        return buf;
    }
}
