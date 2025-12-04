using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using LegodPause.Service.Network;
using LegodPause.Service.Proto;

namespace LegodPause.Service;

#if DEBUG

[TestFixture]
public class Test1
{
    [Test]
    public async Task TestBuffer()
    {
        var data = new ProtoLib() { Message = "Hello" };
        var encoder = new ProtobufMsgEncoder();
        Pipe pipe = new Pipe();
        var writeLength = await encoder.Encode(pipe.Writer, data);
        Console.WriteLine(writeLength);
        var result = await encoder.Decode<ProtoLib>(pipe.Reader);
        Console.WriteLine(result.Message);
    }
}

#endif