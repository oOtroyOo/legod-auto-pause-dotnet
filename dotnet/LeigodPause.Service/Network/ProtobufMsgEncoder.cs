using System.Buffers;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Net;
using LeigodPause.Service.Ipc;

#if True


namespace LeigodPause.Service.Network;

public class ProtobufMsgEncoder(ILogger<ProtobufMsgEncoder>? logger)
{
    private readonly MemoryStream _serializeStream = new MemoryStream();


    private static async Task<byte[]> ReadExactAsync(PipeStream pipe, int count, CancellationToken token)
    {
        var buf = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await pipe.ReadAsync(buf, read, count - read, token).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }

        return buf;
    }

    public async Task<int> Encode<T>(PipeStream pipe, T msgObj, CancellationToken token)
    {
        try
        {
            byte[] bodyBuffer;
            lock (_serializeStream)
            {
                ProtoBuf.Serializer.Serialize(_serializeStream, msgObj);
                bodyBuffer = _serializeStream.ToArray();
            }

            await pipe.WriteAsync(BitConverter.GetBytes(bodyBuffer.Length), 0, 4, token).ConfigureAwait(false);
            await pipe.WriteAsync(bodyBuffer, 0, bodyBuffer.Length, token).ConfigureAwait(false);
            await pipe.FlushAsync(token).ConfigureAwait(false);
            // writer.Advance(bodyBuffer.Length + 8);
            return bodyBuffer.Length + 4;
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Encode failed");
            throw;
        }
        finally
        {
            lock (_serializeStream)
            {
                _serializeStream.SetLength(0);
            }
        }
    }

    public async Task<T> Decode<T>(PipeStream pipe, CancellationToken token)
    {
        try
        {
            var lenBuf = await ReadExactAsync(pipe, 4, token).ConfigureAwait(false);
            var len = BitConverter.ToInt32(lenBuf, 0);
            var body = await ReadExactAsync(pipe, len, token).ConfigureAwait(false);
            return ProtoBuf.Serializer.Deserialize<T>(body);
        }
        finally
        {
        }
    }
}

#endif