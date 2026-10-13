using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using LeigodPause.Service.Api;
using LeigodPause.Service.Proto;
using Timer = System.Timers.Timer;

namespace LeigodPause.Service.Models;

public partial class LeigodCoreModel : ObservableObject
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<LeigodCoreModel> logger;

    // 在构造时（于 UI 线程解析服务）捕获同步上下文；
    // Windows 服务中为 null，此时直接赋值即可（无 UI 需更新）。
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;


    // 防止一次请求未完成时定时器又触发，造成并发请求/重复写属性
    private int _inFlight;

    [ObservableProperty] private string? _token = ConfigFile.GetValue<string>("account_token");
    [ObservableProperty] private string? _userName;
    [ObservableProperty] private string? _pauseStatus;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotPaused))]
    private int? _pauseStatusId;

    public bool IsNotPaused => PauseStatusId == 0;
    public const string Pause_url = "https://webapi.leigod.com/api/user/pause";
    public const string Info_url = "https://webapi.leigod.com/api/user/info";

    [ObservableProperty] private JsonObject? _userInfoJson;

    public LeigodCoreModel(IHttpClientFactory httpClientFactory, ILogger<LeigodCoreModel> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = RequestUserInfo();
    }

    partial void OnTokenChanged(string? oldValue, string? newValue)
    {
        ConfigFile.SetValue("account_token", newValue);
        if (string.IsNullOrEmpty(newValue))
        {
            ApplyFields(null);
        }
    }

    public Task RequestUserInfo(Action<int, string?>? onError = null)
    {
        return PostLeigodAsync(
            token => (
                Info_url,
                new StringContent(new JsonObject
                {
                    { "account_token", token },
                    { "lang", "zh_CN" },
                    { "os_type", 4 },
                }.ToJsonString())),
            jsonObject => HandleInfoResult(jsonObject, onError),
            onError,
            "用户信息获取失败");
    }

    // 统一切回 UI 线程写绑定属性，避免跨线程更新 DependencyProperty
    void ApplyFields(JsonObject? data)
    {
        UserInfoJson = data;
        UserName = data?["nickname"]?.GetValue<string>();
        PauseStatus = data?["pause_status"]?.GetValue<string>();
        PauseStatusId = data?["pause_status_id"]?.GetValue<int>();
    }

    public Task SendPauseState(Action<int, string?>? onError = null)
    {
        // https://webapi.leigod.com/api/user/pause?os_type=5&account_token=xxxx&region_code=1&src_channel=guanwang&lang=zh_CN
        return PostLeigodAsync(
            token => (
                $"{Pause_url}?os_type={4}&account_token={token}&region_code={1}&src_channel=guanwang&lang=zh_CN",
                new StringContent("{}")),
            jsonObject => RaiseError(jsonObject, onError),
            onError,
            "暂停调用失败");
    }

    /// <summary>
    /// 统一的接口调用骨架：在途保护、token 校验、构造并发送请求、解析、分发结果、异常日志。
    /// requestFactory 用最新 token 构造 (url, content)；handleResult 收到解析成功的根 JSON，
    /// 由各调用方决定成功/错误如何处理；onError 在网络/传输层错误时以 -1 回调。
    /// </summary>
    private async Task PostLeigodAsync(
        Func<string, (string url, HttpContent content)> requestFactory,
        Action<JsonObject> handleResult,
        Action<int, string?>? onError,
        string errorLogMessage)
    {
        // 在途保护：已有请求未完成则跳过本次
        if (Interlocked.Exchange(ref _inFlight, 1) == 1)
        {
            return;
        }

        try
        {
            var token = ConfigFile.GetValue<string>("account_token");
            if (token is { Length: > 0 })
            {
                var (url, content) = requestFactory(token);
                var httpClient = httpClientFactory.CreateClient(HttpClientExtensions.LeigodClientName);
                var response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var jsonStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (JsonNode.Parse(jsonStr) is JsonObject jsonObject)
                {
                    handleResult(jsonObject);
                }
            }
        }
        catch (HttpRequestException e)
        {
            // 网络/传输层错误（连接失败、超时、非 2xx 等）：记录日志并回调 onError
            logger.LogError(e, errorLogMessage);
            InvokeOnUi(() => onError?.Invoke(-1, e.Message));
        }
        catch (Exception e)
        {
            logger.LogError(e, errorLogMessage);
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    // user/info：取到用户信息则写属性，否则按错误码回调
    private void HandleInfoResult(JsonObject jsonObject, Action<int, string?>? onError)
    {
        // 优先取 data 内层，扁平结构（无 data 包裹）时退化为根节点
        var data = jsonObject["data"] as JsonObject ?? jsonObject;

        if (data is { Count: > 0 })
        {
            InvokeOnUi(() => ApplyFields(data));
            return;
        }

        RaiseError(jsonObject, onError);
    }

    // code != 0 时通过 onError 回调上报（自动切回 UI 线程）
    private void RaiseError(JsonObject jsonObject, Action<int, string?>? onError)
    {
        var code = jsonObject["code"]?.GetValue<int>();
        if (code is null || code == 0)
        {
            return;
        }

        var msg = jsonObject["msg"]?.GetValue<string>();
        InvokeOnUi(() => onError?.Invoke(code.Value, msg));
    }

    // 统一切回 UI 线程执行，避免跨线程更新 DependencyProperty
    private void InvokeOnUi(Action action)
    {
        if (_uiContext != null)
        {
            _uiContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
}