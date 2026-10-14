using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

public class CustomServiceLifetime(
    IHostEnvironment hostEnvironment,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory,
    IOptions<HostOptions> hostOptions) : WindowsServiceLifetime(hostEnvironment, applicationLifetime, loggerFactory, hostOptions)
{
    private ILogger<CustomServiceLifetime> logger = loggerFactory.CreateLogger<CustomServiceLifetime>();

    public event Action OnShutdownEvents;

    protected override void OnShutdown()
    {
        logger.LogInformation("触发系统关机事件，执行OnShutdown自定义逻辑");
        try
        {
            OnShutdownEvents?.Invoke();
        }
        catch (Exception e)
        {
            logger.LogError(e, "执行关机任务时发生异常");
        }

        // 此处直接放置原.NET Framework服务中OnShutdown()内的业务逻辑
        base.OnShutdown();
    }
}