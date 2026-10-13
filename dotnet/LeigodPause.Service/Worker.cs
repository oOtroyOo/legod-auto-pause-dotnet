using System.Timers;
using LeigodPause.Service.Network;
using LeigodPause.Service.Proto;

namespace LeigodPause.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("games: {games}", ConfigFile.GetValue<string>("games"));
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time:yyyyMMdd HH:mm:ss}", DateTimeOffset.Now);
            }


            await Task.Delay(5000, stoppingToken);
        }
    }
}