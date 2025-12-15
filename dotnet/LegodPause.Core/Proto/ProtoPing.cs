using ProtoBuf;

namespace LegodPause.Core.Proto;

[ProtoContract]
public class ProtoPing
{
    [ProtoMember(1)]
    public long pingTime;
    
}