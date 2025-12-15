using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using Microsoft.Extensions.Logging;

#if True


namespace LegodPause.Core.Network;

public class ProtobufMsgEncoder(ILogger<ProtobufMsgEncoder>? logger)
{
    private readonly MemoryStream _serializeStream = new MemoryStream();
    private const int Hex = 0x12345678;
    private ILogger<ProtobufMsgEncoder>? _logger = logger;


    public async Task<int> Encode<T>(PipeWriter writer, T msgObj)
    {
        try
        {
            Span<byte> bodyBuffer;
            lock (_serializeStream)
            {
                ProtoBuf.Serializer.Serialize(_serializeStream, msgObj);

                bodyBuffer = new Span<byte>(_serializeStream.GetBuffer(), 0, (int)_serializeStream.Position);
            }

            for (int i = 0; i < bodyBuffer.Length; i++)
            {
                bodyBuffer[i] ^= (byte)(Hex >> (8 * (i % 4)));
            }

            writer.Write(BitConverter.GetBytes(bodyBuffer.Length + 4));

            var crcValue = ChecksumUtil.Adler32(bodyBuffer);
            writer.Write(BitConverter.GetBytes(crcValue));
            writer.Write(bodyBuffer);
            // writer.Advance(bodyBuffer.Length + 8);
            return bodyBuffer.Length + 8;
        }
        finally
        {
            lock (_serializeStream)
            {
                _serializeStream.SetLength(0);
            }

            await writer.FlushAsync();
        }
    }

    public async Task<T> Decode<T>(PipeReader reader)
    {
        try
        {
            // Read message length (4 bytes)
            var readerResult = await reader.ReadAtLeastAsync(4);
            if (readerResult.IsCompleted || readerResult.Buffer.Length < 4)
                throw new InvalidDataException("Incomplete message length");

            var length =
#if NETFRAMEWORK
                BitConverter.ToInt32(readerResult.Buffer.Slice(0, 4).ToArray(), 0);
#else
                BitConverter.ToInt32(readerResult.Buffer.Slice(0, 4).FirstSpan);
#endif
            reader.AdvanceTo(readerResult.Buffer.GetPosition(4));

            // Read CRC (4 bytes) and message body
            readerResult = await reader.ReadAtLeastAsync(length);
            if (readerResult.IsCompleted || readerResult.Buffer.Length < length)
                throw new InvalidDataException("Incomplete message body");

            var crc =
#if NETFRAMEWORK
                BitConverter.ToInt32(readerResult.Buffer.Slice(0, 4).ToArray(), 0);
#else
                BitConverter.ToInt32(readerResult.Buffer.Slice(0, 4).FirstSpan);
#endif
            var bodyBuffer = new Span<byte>(new byte[length - 4]);
            readerResult.Buffer.Slice(4, length - 4).CopyTo(bodyBuffer);

            reader.AdvanceTo(readerResult.Buffer.GetPosition(length));

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