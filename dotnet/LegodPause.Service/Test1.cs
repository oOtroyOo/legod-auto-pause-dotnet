using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        var encoder = new ProtobufMsgEncoder(null);
        Pipe pipe = new Pipe();
        Console.WriteLine(await encoder.Encode(pipe.Writer, data));
        Console.WriteLine(await encoder.Encode(pipe.Writer, data2));

        Console.WriteLine((await encoder.Decode<ProtoLib>(pipe.Reader)).Message);
        Console.WriteLine((await encoder.Decode<ProtoLib>(pipe.Reader)).Message);
    }

    [Test]
    public void AllIP()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] ipBytes = ip.Address.GetAddressBytes();
                    byte[] maskBytes = ip.IPv4Mask.GetAddressBytes();
                    byte[] broadcastBytes = new byte[4];

                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                    }

                    IPAddress broadcastAddress = new IPAddress(broadcastBytes);
                    Console.WriteLine($"{networkInterface.Name}: {ip.Address}, {broadcastAddress}");
                }

                else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    byte[] ipBytes = ip.Address.GetAddressBytes();
                    byte[] prefixLength = BitConverter.GetBytes(ip.PrefixLength);
                    byte[] broadcastBytes = new byte[16];

                    for (int i = 0; i < 16; i++)
                    {
                        int maskBit = (i < prefixLength[0] / 8) ? 0xFF :
                            (i == prefixLength[0] / 8) ? (0xFF << (8 - (prefixLength[0] % 8))) : 0x00;
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBit);
                    }

                    IPAddress broadcastAddress = new IPAddress(broadcastBytes);
                    Console.WriteLine($"{networkInterface.Name}: {ip.Address}, {broadcastAddress}");
                }
            }
        }
    }

    [Test]
    public void AllIpBradcast()
    {
        foreach (var address in NetworkUtil.GetAllIPBradcast())
        {
            Console.WriteLine(address);
        }
    }

    [Test]
    public async Task TestUpdClient()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, NetworkServer.UdpPort);
        using var udpClient = new UdpClient(endpoint);
        using var channel = new PipeChannel(CancellationToken.None, null);
        channel.AddSocket(udpClient.Client);
        var protobufMsgEncoder = new ProtobufMsgEncoder(null);
        var receiveResult = await protobufMsgEncoder.Decode<ProtoLib>(channel.Receive.Reader);
        Console.WriteLine(receiveResult.Ping?.pingTime);
    }


    [Test]
    public async Task TestUpdClientV6()
    {
        var endpoint = new IPEndPoint(IPAddress.IPv6Any, NetworkServer.UdpPort);
        using var udpClient = new UdpClient(endpoint);
        using var channel = new PipeChannel(CancellationToken.None, null);
        channel.AddSocket(udpClient.Client);
        var protobufMsgEncoder = new ProtobufMsgEncoder(null);
        var receiveResult = await protobufMsgEncoder.Decode<ProtoLib>(channel.Receive.Reader);
        Console.WriteLine(receiveResult.Ping?.pingTime);
    }
}

#endif