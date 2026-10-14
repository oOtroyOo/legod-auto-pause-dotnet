namespace LeigodPause.Service.Models;

public partial class LeigodCoreModel(IHttpClientFactory httpClientFactory, ILogger<LeigodCoreModel> logger, ConfigFile configFile) : ObservableObject
{
    // 在构造时（于 UI 线程解析服务）捕获同步上下文；
    // Windows 服务中为 null，此时直接赋值即可（无 UI 需更新）。
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;


    // 三类操作各自独立的在途标志，互不阻塞：刷新 / 暂停 / 扫进程。
    private int _infoInFlight;
    private int _pauseInFlight;
    private int _findInFlight;

    // 保护跨线程读写的状态字段（Service 端 Worker 线程 vs IPC 线程）。
    private readonly object _stateLock = new();

    [ObservableProperty] private string? _token = configFile.GetValue<string>("account_token");
    [ObservableProperty] private string? _userName;
    [ObservableProperty] private string? _pauseStatus;
    [ObservableProperty] private string? _matchedGameProcess;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotPaused))]
    private int? _pauseStatusId;

    [ObservableProperty] private JsonObject? _userInfoJson;


    public bool IsNotPaused => PauseStatusId == 0;
    public const string Pause_url = "https://webapi.leigod.com/api/user/pause";
    public const string Info_url = "https://webapi.leigod.com/api/user/info";

    public DateTimeOffset LastRequestInfoTime { get; private set; }
    public DateTimeOffset MachedGameProcessTime { get; private set; }


    private TimeSpan _requestInfoDelay = TimeSpan.FromSeconds(5);
    public bool RequestInfoTimeout => DateTimeOffset.Now - LastRequestInfoTime > _requestInfoDelay;


    partial void OnTokenChanged(string? oldValue, string? newValue)
    {
        configFile.SetValue("account_token", newValue);
        if (string.IsNullOrEmpty(newValue))
        {
            ApplyFields(null);
        }
    }

    public async Task RequestUserInfo(Func<int, string?, Task>? onError = null)
    {
        // 在途保护：刷新与暂停/扫进程互不阻塞
        if (Interlocked.Exchange(ref _infoInFlight, 1) == 1)
        {
            return;
        }

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
                var jsonObject = JsonNode.Parse(jsonStr) as JsonObject;
                var data = jsonObject?["data"] as JsonObject ?? jsonObject;
                if (data is { Count: > 0 })
                {
                    await InvokeOnUi(() =>
                    {
                        try
                        {
                            ApplyFields(data);
                            return Task.CompletedTask;
                        }
                        catch (Exception exception)
                        {
                            return Task.FromException(exception);
                        }
                    });
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
            Interlocked.Exchange(ref _infoInFlight, 0);
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
        // 在途保护：与刷新/扫进程互不阻塞
        if (Interlocked.Exchange(ref _pauseInFlight, 1) == 1)
        {
            return false;
        }

        try
        {
            var token = configFile.GetValue<string>("account_token");
            if (token is { Length: > 0 })
            {
                var httpClient = httpClientFactory.CreateClient(HttpClientExtensions.LeigodClientName);
                // https://webapi.leigod.com/api/user/pause?os_type=5&account_token=xxxx&region_code=1&src_channel=guanwang&lang=zh_CN
                string urlQuery = $"{Pause_url}?os_type={4}&account_token={token}&region_code={1}&src_channel=guanwang&lang=zh_CN";
                var postMessage = await httpClient.PostAsync(urlQuery, new StringContent("{}")).ConfigureAwait(false);
                postMessage.EnsureSuccessStatusCode();
                var jsonStr = await postMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonObject = JsonNode.Parse(jsonStr) as JsonObject;
                var code = jsonObject?["code"]?.GetValue<int>();
                var msg = jsonObject?["msg"]?.GetValue<string>();
                if (code != null && code != 0 && onError != null)
                {
                    await InvokeOnUi(() => onError.Invoke((int)code, msg));
                }
                else
                {
                    MachedGameProcessTime = default;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "暂停调用失败");
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _pauseInFlight, 0);
        }
    }

    public async Task<string> FindGameProcess()
    {
        // 在途保护：避免多次轮询并发扫进程、重复写状态
        if (Interlocked.Exchange(ref _findInFlight, 1) == 1)
        {
            return MatchedGameProcess ?? string.Empty;
        }

        try
        {
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
                        if (IsNotPaused)
                        {
                            MachedGameProcessTime = DateTimeOffset.Now;
                        }

                        MatchedGameProcess = gameMsg;
                    }

                    return gameMsg;
                }
                finally
                {
                    // 释放进程句柄，避免每次扫描泄漏
                    foreach (var p in processesByName)
                    {
                        p.Dispose();
                    }
                }
            }

            lock (_stateLock)
            {
                MatchedGameProcess = string.Empty;
            }

            return string.Empty;
        }
        finally
        {
            Interlocked.Exchange(ref _findInFlight, 0);
        }
    }

    // 统一切回 UI 线程执行，避免跨线程更新 DependencyProperty
    private async Task InvokeOnUi(Action action)
    {
        if (_uiContext != null)
        {
            var wait = new TaskCompletionSource<object?>();
            _uiContext.Post(_ =>
            {
                action();
                wait.SetResult(null);
            }, null);
            await wait.Task;
        }
        else
        {
            action();
        }
    }

    // 统一切回 UI 线程执行，避免跨线程更新 DependencyProperty；
    // 正确传播 action 内部异常（不再用 ContinueWith 静默吞掉）。
    private async Task InvokeOnUi(Func<Task> action)
    {
        if (_uiContext != null)
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