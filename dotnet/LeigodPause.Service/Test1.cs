namespace LeigodPause.Service;

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
        Console.WriteLine("OEMCP=" + Utils.GetOEMCP());
        var oemEncoding = Utils.GetOEMEncoding();
        Console.WriteLine($"OEMEncoding={oemEncoding} codePage={oemEncoding.CodePage} name=\"{oemEncoding.EncodingName}\" ");
        var consoleEncoding = Utils.GetConsoleEncoding();
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
}

#endif