using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LegodPause.Core;
using LegodPause.Core.Network;
using LegodPause.Service.Network;
using LegodPause.Service.Proto;

namespace LegodPause.Service;

#if DEBUG

[TestFixture]
public class Test1
{
    public Test1()
    {
#if NETCOREAPP
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
    }

    [Test]
    public void TestEncoding()
    {
        /*
         *
繁体中文
OEMCP=950
OEMEncoding=System.Text.DBCSCodePageEncoding codePage=950 name="Chinese Traditional (Big5)"
ConsoleEncoding=System.Text.DBCSCodePageEncoding codePage=950 name="Chinese Traditional (Big5)"

简体中文
OEMCP=936
OEMEncoding=System.Text.DBCSCodePageEncoding codePage=936 name="Chinese Simplified (GB2312)"
ConsoleEncoding=System.Text.DBCSCodePageEncoding codePage=936 name="Chinese Simplified (GB2312)"
[SC] OpenService 失败 1060:

日语
OEMCP=932
OEMEncoding=System.Text.DBCSCodePageEncoding codePage=932 name="日本語 (シフト JIS)"
ConsoleEncoding=System.Text.DBCSCodePageEncoding codePage=932 name="日本語 (シフト JIS)"
[SC] OpenService FAILED 1060:

         */
        Console.WriteLine("OEMCP=" + PlatformUtils.GetOEMCP());
        var oemEncoding = PlatformUtils.GetOEMEncoding();
        Console.WriteLine($"OEMEncoding={oemEncoding} codePage={oemEncoding.CodePage} name=\"{oemEncoding.EncodingName}\" ");
        var consoleEncoding = PlatformUtils.GetConsoleEncoding();
        Console.WriteLine($"ConsoleEncoding={consoleEncoding} codePage={consoleEncoding.CodePage} name=\"{consoleEncoding.EncodingName}\"");
        // cmd /c "exit"
        var process = Process.Start(new ProcessStartInfo("sc.exe", $"""
                                                                    qc 000
                                                                    """)
        {
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = consoleEncoding,
        });
        process.WaitForExit();
        var output = process.StandardOutput.ReadToEnd();
        Console.WriteLine(output);
        Assert.That(output.Contains("指定的服务未安装"));
    }

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
        ProtoPing? protoPing = null;
        do
        {
            var receiveResult = await protobufMsgEncoder.Decode<ProtoLib>(channel.Receive.Reader);
            protoPing = receiveResult.Ping;
            if (protoPing != null)
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(protoPing.pingTime).ToLocalTime();
                Console.WriteLine($"{protoPing.pingTime}={date:yyyy-MM-dd HH:mm:ss.fff}");
            }
        } while (protoPing is null);
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