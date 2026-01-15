using LegodPause.Core.Network;
using Microsoft.Extensions.DependencyInjection;

namespace LegodPause.Core;

public static class HostingExtension
{
    public static IServiceCollection AddLegodPuseCore(this IServiceCollection services)
    {
        services.AddTransient<TcpClientMessenger>();
        services.AddTransient<UdpClientMessenger>();
        return services;
    }
}