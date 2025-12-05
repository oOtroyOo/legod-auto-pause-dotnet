using ProtoBuf;

namespace LegodPause.Service.Proto;

[ProtoContract]
public class ProtoPing
{
    [ProtoMember(1)]
    public long pingTime;
}