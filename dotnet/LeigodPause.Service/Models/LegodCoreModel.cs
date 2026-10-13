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

    public async Task RequestUserInfo(Action<int, string?>? onError = null)
    {
        // 在途保护：已有请求则跳过本次轮询
        if (Interlocked.Exchange(ref _inFlight, 1) == 1)
        {
            return;
        }

        try
        {
            var token = ConfigFile.GetValue<string>("account_token");
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
                    if (_uiContext != null)
                    {
                        _uiContext.Post(_ => ApplyFields(data), null);
                    }
                    else
                    {
                        ApplyFields(data);
                    }
                }
                else
                {
                    var code = jsonObject?["code"]?.GetValue<int>();
                    var msg = jsonObject?["msg"]?.GetValue<string>();
                    if (code != null && code != 0)
                    {
                        if (_uiContext != null)
                        {
                            _uiContext.Post(_ => onError?.Invoke((int)code, msg), null);
                        }
                        else
                        {
                            onError?.Invoke((int)code, msg);
                        }
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
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    // 统一切回 UI 线程写绑定属性，避免跨线程更新 DependencyProperty
    void ApplyFields(JsonObject? data)
    {
        UserInfoJson = data;
        UserName = data?["nickname"]?.GetValue<string>();
        PauseStatus = data?["pause_status"]?.GetValue<string>();
        PauseStatusId = data?["pause_status_id"]?.GetValue<int>();
    }

    public async Task SendPauseState(Action<int, string?>? onError = null)
    {
        // 在途保护：已有请求则跳过本次轮询
        if (Interlocked.Exchange(ref _inFlight, 1) == 1)
        {
            return;
        }

        try
        {
            var token = ConfigFile.GetValue<string>("account_token");
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
                if (code != null && code != 0)
                {
                    if (_uiContext != null)
                    {
                        _uiContext.Post(_ => onError?.Invoke((int)code, msg), null);
                    }
                    else
                    {
                        onError?.Invoke((int)code, msg);
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "暂停调用失败");
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }
}