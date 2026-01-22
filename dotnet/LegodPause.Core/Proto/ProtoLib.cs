using System.IO;
using LegodPause.Core.Network;
using ProtoBuf;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace LegodPause.Core.Proto;

[ProtoContract]
public class ProtoLib : IRequestInfo, IRequestInfoBuilder
{
    static long _seq = 0;
    [ProtoMember(1, IsRequired = false)] public long? Seq = ++_seq;
    [ProtoMember(2, IsRequired = false)] public string? Error;
    [ProtoMember(3, IsRequired = false)] public string? Message;

    [ProtoMember(4, IsRequired = false)] public string? Cmd;
    [ProtoMember(5, IsRequired = false)] public string? Key;
    [ProtoMember(6, IsRequired = false)] public string? Str;
    [ProtoMember(7, IsRequired = false)] public int? Num;

    [ProtoMember(8, IsRequired = false)] public ProtoPing? Ping;
    [ProtoMember(9, IsRequired = false)] public ProtoServerInfo? ServerInfo;

    public void Build<TWriter>(ref TWriter writer) where TWriter : IBytesWriter
    {
        using MemoryStream _serializeStream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(_serializeStream, this);
        _serializeStream.TryGetBuffer(out var buffer);
        var span = (new byte[4]);
        TouchSocketBitConverter.BigEndian.GetBytes((ushort)(buffer.Count)).CopyTo(span);
        writer.Write(span);
        writer.Write(buffer);
        var crc16Value = Crc.Crc16Value(buffer.AsSpan(0, buffer.Count));
        writer.Write(TouchSocketBitConverter.BigEndian.GetBytes(crc16Value).Span);
    }

    public int MaxLength { get; }
}