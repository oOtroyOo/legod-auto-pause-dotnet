using ProtoBuf;

namespace LegodPause.Core.Network;

[ProtoContract]
public class ProtoServerInfo
{
    [ProtoMember(1)] public string[]? IpAddress;
    [ProtoMember(2)] public int? TcpPort;
    [ProtoMember(3)] public int? UdpPort;
}