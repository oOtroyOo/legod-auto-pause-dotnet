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
        var data = new ProtoLib() { Message = "Hello", Seq = 1 };
        var data2 = new ProtoLib() { Message = "World", Seq = 2 };
        var encoder = new ProtobufMsgEncoder();
        Pipe pipe = new Pipe();
        Console.WriteLine(await encoder.Encode(pipe.Writer, data));
        Console.WriteLine(await encoder.Encode(pipe.Writer, data2));

        Console.WriteLine((await encoder.Decode<ProtoLib>(pipe.Reader)).Message);
        Console.WriteLine((await encoder.Decode<ProtoLib>(pipe.Reader)).Message);
    }
}

#endif