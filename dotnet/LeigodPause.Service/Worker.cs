using System.Timers;
using LeigodPause.Service.Ipc;
using LeigodPause.Service.Models;
using LeigodPause.Service.Network;
using LeigodPause.Service.Proto;

namespace LeigodPause.Service;

public class Worker(ILogger<Worker> logger, ServiceIpcServer ipcServer, LeigodCoreModel leigodCoreModel) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("games: {games}", ConfigFile.GetValue<string>("games"));
        ipcServer.Start();
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        ipcServer.Stop();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time:yyyyMMdd HH:mm:ss}", DateTimeOffset.Now);
            }


            try
            {
                await UpdateTimer();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Worker error");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task UpdateTimer()
    {
        await RequestUserInfo();
    }

    private async Task RequestUserInfo()
    {
        var token = ConfigFile.GetValue<string>("account_token");
        if (token is { Length: > 0 })
        {
            if (leigodCoreModel.RequestInfoTimeout)
            {
                await leigodCoreModel.RequestUserInfo((c, m) =>
                {
                    // leigodCoreModel.Token = null;
                    ipcServer.LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
                }).ConfigureAwait(false);
            }
        }
    }
}