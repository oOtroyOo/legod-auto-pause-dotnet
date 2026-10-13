using System.Timers;
using LeigodPause.Service.Ipc;
using LeigodPause.Service.Network;
using LeigodPause.Service.Proto;

namespace LeigodPause.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceIpcServer _ipcServer;

    public Worker(ILogger<Worker> logger, ServiceIpcServer ipcServer)
    {
        _logger = logger;
        _ipcServer = ipcServer;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("games: {games}", ConfigFile.GetValue<string>("games"));
        _ipcServer.Start();
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _ipcServer.Stop();
        await base.StopAsync(cancellationToken);
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