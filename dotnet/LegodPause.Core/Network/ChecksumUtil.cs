namespace LegodPause.Core.Network;

public class ChecksumUtil
{

    public static uint Adler32(Memory<byte> data, uint start = 0, uint len = 0)
    {
        return Adler32(data.Span, start, len);
    }

    public static uint Adler32(ReadOnlySpan<byte> data, uint start = 0, uint len = 0)
    {
        if (start >= (uint)data.Length)
        {
            start = (uint)data.Length;
        }

        if (len == 0)
        {
            len = (uint)data.Length - start;
        }

        if (len + start > (uint)data.Length)
        {
            len = (uint)data.Length - start;
        }

        uint i = start;
        uint a = 1;
        uint b = 0;

        while (i < (start + len))
        {
            a = (a + data[(int)i]) % 65521;
            b = (a + b) % 65521;
            i++;
        }

        return (b << 16) | a;
    }
}