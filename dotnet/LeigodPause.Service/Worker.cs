using Microsoft.Win32;

namespace LeigodPause.Service;

public class Worker(ILogger<Worker> logger, ServiceIpcServer ipcServer, LeigodCoreModel leigodCoreModel) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("StopAsync");
        // 注销事件，避免内存泄漏
        SystemEvents.SessionEnding -= OnSessionEnding;
        await ipcServer.Stop();
        try
        {
            var hostLifetime = Program.AppHost.Services.GetRequiredService<IHostLifetime>();
            if (hostLifetime is CustomServiceLifetime customLifetime)
            {
                customLifetime.OnShutdownEvents -= OnHostLifetimeShutdown;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "获取不到Lifetime实例");
        }

        await OnEndingActionAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExecuteAsync");
        logger.LogInformation("games: {games}", ConfigFile.GetValue<string>("games"));
        ipcServer.Start();
        SystemEvents.SessionEnding += OnSessionEnding;
        try
        {
            var hostLifetime = Program.AppHost.Services.GetRequiredService<IHostLifetime>();
            logger.LogInformation("IHostLifetime is " + hostLifetime.GetType().FullName);
            if (hostLifetime is CustomServiceLifetime customLifetime)
            {
                customLifetime.OnShutdownEvents += OnHostLifetimeShutdown;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "获取不到Lifetime实例");
        }

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

            float updateTime = ConfigFile.GetValue<float>("update");
            if (updateTime < 1) updateTime = 1;

            await Task.Delay((int)(updateTime * 1000), stoppingToken);
        }

        logger.LogInformation("ExecuteAsync 的token已取消");
        await OnEndingActionAsync();
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        logger.LogInformation("OnSessionEnding");
        _ = OnEndingActionAsync();
    }

    private void OnHostLifetimeShutdown()
    {
        logger.LogInformation("OnHostLifetimeShutdown");
        _ = OnEndingActionAsync();
    }

/*
2026-10-14 14:23:07.173 [Information] CustomServiceLifetime: 触发系统关机事件，执行OnShutdown自定义逻辑
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: OnHostLifetimeShutdown
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: OnEndingAction
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: SendPauseState
2026-10-14 14:23:07.174 [Information] LeigodPause.Service.Worker: SendPauseState Finish
2026-10-14 14:23:07.175 [Information] Microsoft.Hosting.Lifetime: Application is shutting down...
2026-10-14 14:23:07.185 [Information] LeigodPause.Service.Worker: StopAsync
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Ipc.ServiceIpcServer: IPC pipe server stopping
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: OnEndingAction
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: SendPauseState
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: SendPauseState Finish
*/
    /// <summary>
    /// 关机/登出时执行暂停。非阻塞、带超时，避免 .Wait() 阻塞系统关机流程。
    /// </summary>
    private async Task OnEndingActionAsync()
    {
        logger.LogInformation("OnEndingAction");
        try
        {
            var pauseTask = SendPauseState();
            await Task.WhenAny(pauseTask, Task.Delay(3000));
        }
        catch (Exception e)
        {
            logger.LogError(e, "OnEndingAction SendPauseState 失败");
        }
    }

    private async Task UpdateTimer()
    {
        if (leigodCoreModel.RequestInfoTimeout)
        {
            await RequestUserInfo();
            if (leigodCoreModel.IsNotPaused)
            {
                var matched = await leigodCoreModel.FindGameProcess();
                if (string.IsNullOrEmpty(matched) && leigodCoreModel.MachedGameProcessTime != default)
                {
                    var loopTime = ConfigFile.GetValue<float>("looptime");
                    if (loopTime < 1) loopTime = 1;
                    if (DateTimeOffset.Now - leigodCoreModel.MachedGameProcessTime > TimeSpan.FromSeconds(loopTime))
                    {
                        await SendPauseState();
                    }
                }
            }
        }
    }

    private async Task RequestUserInfo()
    {
        var token = ConfigFile.GetValue<string>("account_token");
        if (token is { Length: > 0 })
        {
            await leigodCoreModel.RequestUserInfo((c, m) =>
            {
                // leigodCoreModel.Token = null;
                ipcServer.LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private async Task SendPauseState()
    {
        logger.LogInformation("SendPauseState");
        var token = ConfigFile.GetValue<string>("account_token");
        if (token is { Length: > 0 })
        {
            await leigodCoreModel.SendPauseState((c, m) =>
            {
                // leigodCoreModel.Token = null;
                ipcServer.LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }

        logger.LogInformation("SendPauseState Finish");
    }
}