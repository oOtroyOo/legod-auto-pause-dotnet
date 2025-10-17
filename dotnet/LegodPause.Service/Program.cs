using System.Diagnostics;
using System.Text;

namespace LegodPause.Service;

public class Program
{
    public static void Main(string[] args)
    {
#if NET
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();



#if NETFRAMEWORK
        Console.WriteLine();
#endif
    }

}