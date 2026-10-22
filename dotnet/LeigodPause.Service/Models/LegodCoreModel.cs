using System.Collections.ObjectModel;

namespace LeigodPause.Service.Models;

public partial class LeigodCoreModel : ObservableObject
{
    // 在构造时（于 UI 线程解析服务）捕获同步上下文；
    // Windows 服务中为 null，此时直接赋值即可（无 UI 需更新）。
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private readonly int threadId = Thread.CurrentThread.ManagedThreadId;


    // 三类操作各自独立的在途标志，互不阻塞：刷新 / 暂停 / 扫进程。
    private SemaphoreSlim _infoInFlight = new(1, 1);
    private SemaphoreSlim _pauseInFlight = new(1, 1);
    private SemaphoreSlim _findInFlight = new(1, 1);

    // 保护跨线程读写的状态字段（Service 端 Worker 线程 vs IPC 线程）。
    private readonly object _stateLock = new();

    public string? Token
    {
        get => configFile?.GetValue<string>("account_token") ?? string.Empty;
        set => configFile?.SetValue("account_token", value);
    }

    [ObservableProperty] private string? _userName;
    [ObservableProperty] private string? _pauseStatus;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsMatchedGameProcess))]
    private string? _matchedGameProcess;

    public bool IsMatchedGameProcess => !string.IsNullOrEmpty(MatchedGameProcess);

    private Process? gameProcess;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotPaused))]
    private int? _pauseStatusId;

    public bool IsNotPaused => PauseStatusId == 0;


    [ObservableProperty] private JsonObject? _userInfoJson;

    public ObservableCollection<string> GameListSource { get; }


    public const string Pause_url = "https://webapi.leigod.com/api/user/pause";
    public const string Info_url = "https://webapi.leigod.com/api/user/info";

    public DateTimeOffset LastRequestInfoTime { get; private set; }
    public DateTimeOffset MatchedGameProcessExitTime { get; private set; }


    private TimeSpan _requestInfoDelay = TimeSpan.FromSeconds(5);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<LeigodCoreModel> logger;
    private readonly ConfigFile configFile;

    public LeigodCoreModel(IHttpClientFactory httpClientFactory, ILogger<LeigodCoreModel> logger, ConfigFile configFile)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.configFile = configFile;
        GameListSource = new ObservableCollection<string>(configFile.GetValue<string>("games")?.Split(',') ?? []);
        GameListSource.CollectionChanged += (_, _) => { configFile.SetValue("games", string.Join(",", GameListSource)); };
    }

    public bool RequestInfoTimeout => DateTimeOffset.Now - LastRequestInfoTime > _requestInfoDelay;


    public async Task RequestUserInfo(Func<int, string?, Task>? onError = null)
    {
        // 在途保护：刷新与暂停/扫进程互不阻塞
        await _infoInFlight.WaitAsync().ConfigureAwait(false);

        try
        {
            var token = configFile.GetValue<string>("account_token");
            if (token is { Length: > 0 })
            {
                var httpClient = httpClientFactory.CreateClient(HttpClientExtensions.LeigodClientName);
                var payload = new JsonObject
                {
                    { "account_token", token },
                    { "lang", "zh_CN" },
                    { "os_type", 4 },
                }.ToJsonString();

                var postMessage = await httpClient.PostAsync(Info_url, new StringContent(payload)).ConfigureAwait(false);
                postMessage.EnsureSuccessStatusCode();
                var jsonStr = await postMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                logger.LogInformation("RequestUserInfo jsonStr: {0}", jsonStr);
                var jsonObject = JsonNode.Parse(jsonStr) as JsonObject;
                var data = jsonObject?["data"] as JsonObject ?? jsonObject;
                if (data is { Count: > 0 })
                {
                    ApplyFields(data);
                }
                else
                {
                    var code = jsonObject?["code"]?.GetValue<int>();
                    var msg = jsonObject?["msg"]?.GetValue<string>();
                    if (code != null && code != 0 && onError != null)
                    {
                        await InvokeOnUi(() => onError.Invoke((int)code, msg));
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "用户信息获取失败");
        }
        finally
        {
            _infoInFlight.Release();
            LastRequestInfoTime = DateTimeOffset.Now;
        }
    }

    // 统一切回 UI 线程写绑定属性，避免跨线程更新 DependencyProperty
    public void ApplyFields(JsonObject? data)
    {
        lock (_stateLock)
        {
            UserInfoJson = data;
            UserName = data?["nickname"]?.GetValue<string>();
            PauseStatus = data?["pause_status"]?.GetValue<string>();
            PauseStatusId = data?["pause_status_id"]?.GetValue<int>();
        }
    }

    /// <summary>
    /// 调用暂停接口。返回 true 表示本次真正执行；false 表示在途跳过（已有暂停请求未完成）。
    /// 调用方（DoPauseAsync）据此以明确码告知客户端，而不是伪装成功。
    /// </summary>
    public async Task<bool> SendPauseState(Func<int, string?, Task>? onError = null)
    {
        logger.LogInformation("SendPauseState");
        // 在途保护：与刷新/扫进程互不阻塞
        await _pauseInFlight.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                logger.LogInformation("SendPauseState IsNotPaused 1: {0}", IsNotPaused);
                if (!IsNotPaused)
                {
                    logger.LogInformation("没有暂停状态，请求用户信息");
                    await RequestUserInfo();
                }

                logger.LogInformation("SendPauseState IsNotPaused 2: {0}", IsNotPaused);
                if (!IsNotPaused)
                {
                    logger.LogInformation("没有暂停状态，直接return");
                    MatchedGameProcessExitTime = default;
                    return false;
                }
            }
            catch
            {
                //
            }

            try
            {
                var token = configFile.GetValue<string>("account_token");
                if (token is { Length: > 0 })
                {
                    var httpClient = httpClientFactory.CreateClient(HttpClientExtensions.LeigodClientName);
                    // https://webapi.leigod.com/api/user/pause?os_type=5&account_token=xxxx&region_code=1&src_channel=guanwang&lang=zh_CN
                    string urlQuery = $"{Pause_url}?os_type={4}&account_token={token}&region_code={1}&src_channel=guanwang&lang=zh_CN";
                    logger.LogInformation("Post : {0}", urlQuery);
                    var postMessage = await httpClient.PostAsync(urlQuery, new StringContent("{}")).ConfigureAwait(false);
                    postMessage.EnsureSuccessStatusCode();
                    var jsonStr = await postMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    logger.LogInformation("PostResult : {0}", jsonStr);
                    var jsonObject = JsonNode.Parse(jsonStr) as JsonObject;
                    var code = jsonObject?["code"]?.GetValue<int>();
                    var msg = jsonObject?["msg"]?.GetValue<string>();
                    if (code != null && code != 0 && onError != null)
                    {
                        await InvokeOnUi(() => onError.Invoke((int)code, msg));
                    }
                    else
                    {
                        // 暂停成功执行后清除退出计时，避免重复触发；下次游戏退出会重新计时
                        logger.LogInformation("暂停成功执行后清除退出计时");
                        MatchedGameProcessExitTime = default;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "暂停调用失败");
                return false;
            }
        }
        finally
        {
            _pauseInFlight.Release();
        }
    }

    public async Task<string> FindGameProcess()
    {
        // 在途保护：避免多次轮询并发扫进程、重复写状态
        await _findInFlight.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                if (gameProcess is { HasExited: false })
                {
                    if (MatchedGameProcess?.Equals(Path.GetFileNameWithoutExtension(gameProcess.ProcessName)) == true)
                    {
                        // 游戏仍在运行，尚未退出，清除退出时间
                        MatchedGameProcessExitTime = default;
                        return MatchedGameProcess;
                    }
                }
            }
            catch
            {
                //
            }

            gameProcess?.Dispose();


            string[] games = configFile.GetValue<string>("games")?.Split(',') ?? Array.Empty<string>();
            foreach (var gameName in games)
            {
                var name = gameName.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                } // Process.GetProcessesByName 只接受不含扩展名的进程名，先去掉 .exe 后缀

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - ".exe".Length);
                }

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var processesByName = Process.GetProcessesByName(name);
                try
                {
                    if (processesByName.Length <= 0)
                    {
                        continue;
                    }

                    // 权限不足时读取 MainModule 可能抛异常，仅用进程名兜底，避免中断整个扫描
                    string? fileName = null;
                    try
                    {
                        fileName = processesByName[0].MainModule?.FileName;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "读取进程路径失败（权限不足），仅使用进程名");
                    }

                    string gameMsg = fileName != null ? $"{name}\n{fileName}" : name;
                    logger.LogInformation($"FindGameProcess {gameMsg}");

                    lock (_stateLock)
                    {
                        // 重新匹配到游戏，清除之前的退出时间
                        MatchedGameProcessExitTime = default;
                        gameProcess = processesByName[0];
                        _ = InvokeOnUi(() => MatchedGameProcess = gameMsg);
                    }

                    return gameMsg;
                }
                finally
                {
                    // 释放进程句柄，避免每次扫描泄漏
                    foreach (var p in processesByName)
                    {
                        if (p != gameProcess)
                            p.Dispose();
                    }
                }
            }

            lock (_stateLock)
            {
                // 之前有匹配游戏，现在扫描为空（游戏已退出）：首次进入该状态时记录退出时间，
                // 作为 looptime 宽限计时的起点（避免游戏运行很久后一关闭就立刻暂停）。
                if (MatchedGameProcessExitTime == default && !string.IsNullOrEmpty(MatchedGameProcess))
                {
                    MatchedGameProcessExitTime = DateTimeOffset.Now;
                }

                _ = InvokeOnUi(() => MatchedGameProcess = string.Empty);
            }

            return string.Empty;
        }
        finally
        {
            _findInFlight.Release();
        }
    }

    // 统一切回 UI 线程执行，避免跨线程更新 DependencyProperty
    private async Task InvokeOnUi(Action action)
    {
        await InvokeOnUi(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    // 统一切回 UI 线程执行，避免跨线程更新 DependencyProperty；
    // 正确传播 action 内部异常（不再用 ContinueWith 静默吞掉）。
    private async Task InvokeOnUi(Func<Task> action)
    {
        if (_uiContext != null && threadId != Thread.CurrentThread.ManagedThreadId)
        {
            var wait = new TaskCompletionSource<object?>();
            _uiContext.Post(async _ =>
            {
                try
                {
                    await action();
                    wait.SetResult(null);
                }
                catch (Exception ex)
                {
                    wait.SetException(ex);
                }
            }, null);
            await wait.Task;
        }
        else
        {
            await action();
        }
    }
}