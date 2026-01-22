using System.Net;
using LegodPause.Core.Proto;
using TouchSocket.Core;

namespace LegodPause.Core.Network;
// https://github.com/RRQM/TouchSocket/blob/master/examples/Adapter/CustomAdapterConsoleApp/Program.cs

public class ProtoPackageAdapter : CustomDataHandlingAdapter<ProtoLib>
{
 
    private ushort m_payloadLength;

    protected override FilterResult Filter<TReader>(ref TReader reader, bool beCached, ref ProtoLib request)
    {
        if (beCached)
        {
            //说明上次已经解析了header

            return this.ParseData(ref reader, ref request);
        }
        else
        {
            //首次解析

            if (reader.BytesRemaining < 4)
            {
                //如果剩余数据小于4个字节，则继续等待
                return FilterResult.Cache;
            }

            //读取前4个字节
            var header = reader.GetSpan(4);

            //推进已读取的4个字节
            reader.Advance(4);


            //获取载荷长度
            this.m_payloadLength = TouchSocketBitConverter.BigEndian.To<ushort>(header);

            return this.ParseData(ref reader, ref request);
        }
    }

    private FilterResult ParseData<TReader>(ref TReader reader, ref ProtoLib myDataClass)
        where TReader : IBytesReader
    {
        //判断剩余数据是否足够，+2是因为最后2个字节是CRC16校验码
        if (reader.BytesRemaining < this.m_payloadLength + 2)
        {
            return FilterResult.Cache;
        }

        //读取数据
        var data = reader.GetSpan(this.m_payloadLength);
        reader.Advance(this.m_payloadLength);

        //读取CRC16校验码
        var crcData = reader.GetSpan(2);
        reader.Advance(2);

        //转换CRC16校验码为ushort，相比于byte[]，更节省内存
        var crc16 = TouchSocketBitConverter.BigEndian.To<ushort>(crcData);

        //计算CRC16
        var newCrc16 = Crc.Crc16Value(data);
        if (crc16 != newCrc16)
        {
            //CRC校验失败
            throw new Exception("CRC校验失败");
        }

        myDataClass = ProtoBuf.Serializer.Deserialize<ProtoLib>(data);

        //至此，数据接收完成，可以进行投递处理
        this.m_payloadLength = 0;
        return FilterResult.Success;
    }
}