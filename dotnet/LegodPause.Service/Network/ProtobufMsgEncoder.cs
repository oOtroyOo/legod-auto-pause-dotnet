using System.Buffers;
using System.IO.Pipelines;
using System.Net;

#if True


namespace LegodPause.Service.Network;

public class ProtobufMsgEncoder
{
    private readonly MemoryStream _serializeStream = new MemoryStream();
    private readonly byte[] _intBuffer = new byte[4];
    private const int Hex = 0x12345678;

    public async Task<int> Encode<T>(PipeWriter writer, T msgObj)
    {
        try
        {
            ProtoBuf.Serializer.Serialize(_serializeStream, msgObj);
            var bodyBuffer = new Span<byte>(_serializeStream.GetBuffer(), 0,  (int)_serializeStream.Position);
            for (int i = 0; i < bodyBuffer.Length; i++)
            {
                bodyBuffer[i] ^= (byte)(Hex >> (8 * (i % 4)));
            }

            writer.Write(BitConverter.GetBytes(bodyBuffer.Length));

            var crcValue = ChecksumUtil.Adler32(bodyBuffer);
            writer.Write(BitConverter.GetBytes(crcValue));
            writer.Write(bodyBuffer);
            // writer.Advance(bodyBuffer.Length + 8);

            return bodyBuffer.Length + 8;
        }
        finally
        {
            _serializeStream.SetLength(0);
            await writer.FlushAsync();
        }
    }

    public async Task<T> Decode<T>(PipeReader reader)
    {
        try
        {
            // Read message length (4 bytes)
            var lengthResult = await reader.ReadAtLeastAsync(4);
            if (lengthResult.IsCompleted || lengthResult.Buffer.Length < 4)
                throw new InvalidDataException("Incomplete message length");

            var length = BitConverter.ToUInt32(lengthResult.Buffer.Slice(0, 4).ToArray(), 0);
            reader.AdvanceTo(lengthResult.Buffer.GetPosition(4));

            // Read CRC (4 bytes) and message body
            var bodyResult = await reader.ReadAtLeastAsync((int)length + 4);
            if (bodyResult.IsCompleted || bodyResult.Buffer.Length < length + 4)
                throw new InvalidDataException("Incomplete message body");

            var crc = BitConverter.ToUInt32(bodyResult.Buffer.Slice(0, 4).ToArray(), 0);
            var bodyBuffer = bodyResult.Buffer.Slice(4, (int)length).ToArray();
            reader.AdvanceTo(bodyResult.Buffer.GetPosition(length + 4));

            // Verify checksum
            var calculatedCrc = ChecksumUtil.Adler32(bodyBuffer);
            if (crc != calculatedCrc)
                throw new InvalidDataException("CRC check failed");

            // Decrypt body
            for (int i = 0; i < bodyBuffer.Length; i++)
            {
                bodyBuffer[i] ^= (byte)(Hex >> (8 * (i % 4)));
            }

            return ProtoBuf.Serializer.Deserialize<T>(bodyBuffer);
        }
        finally
        {
        }
    }
}

#endif