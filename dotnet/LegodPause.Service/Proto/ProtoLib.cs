using LegodPause.Service.Network;
using ProtoBuf;

namespace LegodPause.Service.Proto;

[ProtoContract]
public class ProtoLib
{
    [ProtoMember(1, IsRequired = false)] public long? Seq;
    [ProtoMember(2, IsRequired = false)] public string? Error;
    [ProtoMember(3, IsRequired = false)] public string? Message;

    [ProtoMember(4, IsRequired = false)] public string? Cmd;
    [ProtoMember(5, IsRequired = false)] public string? Key;
    [ProtoMember(6, IsRequired = false)] public string? Str;
    [ProtoMember(7, IsRequired = false)] public int? Num;

    [ProtoMember(8, IsRequired = false)] public ProtoPing? Ping;
    [ProtoMember(9, IsRequired = false)] public ProtoServerInfo? ServerInfo;
}