using System.Timers;
using LeigodPause.Service.Network;
using LeigodPause.Service.Proto;

namespace LeigodPause.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly NetworkServer _networkServer;

    public Worker(ILogger<Worker> logger, NetworkServer networkServer)
    {
        _logger = logger;
        _networkServer = networkServer;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _networkServer.Start(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

         
            await Task.Delay(1000, stoppingToken);
        }
    }
}