using System.IO;
using System.IO.Pipes;
using System.Text;
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
    Ping,
    StartPause,
    StopPause,
    RefreshUserInfo,
    ReloadConfig,
    Exit,
}

[ProtoBuf.ProtoContract]
public class IpcEnvelope
{
    [ProtoBuf.ProtoMember(1, IsRequired = false)]
    public ServiceCommand Command;

    [ProtoBuf.ProtoMember(2, IsRequired = false)]
    public int Code = 0;

    [ProtoBuf.ProtoMember(3, IsRequired = false)]
    public string? Msg;

    [ProtoBuf.ProtoMember(4, IsRequired = false)]
    public byte[]? Payload;

    public override string ToString()
    {
        return $"{Command}:({Code}, {Msg})\n{(Payload == null ? string.Empty : Encoding.UTF8.GetString(Payload))}";
    }
}