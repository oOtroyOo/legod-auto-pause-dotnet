using ProtoBuf;

namespace LegodPause.Service.Proto;

[ProtoContract]
public class ProtoLib
{
    [ProtoMember(1, IsRequired = false)] public uint? Seq;
    [ProtoMember(2, IsRequired = false)] public string? Error;
    [ProtoMember(3, IsRequired = false)] public string? Message;
}