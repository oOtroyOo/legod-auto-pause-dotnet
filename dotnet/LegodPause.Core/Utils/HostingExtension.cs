using LegodPause.Core.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace LegodPause.Core;

public static class HostingExtension
{
    const string UserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";

    public static IServiceCollection AddLegodPuseCore(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationRoot>(_ => new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddIniFile("config.ini", optional: true, reloadOnChange: true)
            .Build());
        services.AddHttpClient();
        services.AddHttpClient("Mobile", configureClient: client => { client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });

        return services;
    }

    public static IPluginManager AddLegodPusePlugin(this IPluginManager a)
    {
        a.AddTcpReceivedPlugin(TcpRecievedTask);
        return a;
    }

    private static async Task TcpRecievedTask(ITcpSession arg1, ReceivedDataEventArgs arg2)
    {
        await arg2.InvokeNext();
    }
}