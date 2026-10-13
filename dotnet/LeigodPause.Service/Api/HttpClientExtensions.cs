using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LeigodPause.Service.Api;

/// <summary>
/// HttpClient 扩展：封装雷神(leigod)接口所需的默认请求头与注册方式。
/// </summary>
public static class HttpClientExtensions
{
    public const string LeigodClientName = "leigod";
    public const string UserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";

    /// <summary>
    /// 以 "leigod" 命名注册一个带默认请求头的 HttpClient（基于 HttpClientFactory）。
    /// </summary>
    public static IServiceCollection AddLeigodHttpClient(
        this IServiceCollection services,
        string name = LeigodClientName)
    {
        services.AddHttpClient(name, client => client.ApplyLeigodDefaultHeaders())
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                         | System.Net.DecompressionMethods.Deflate
            });
        return services;
    }

    /// <summary>
    /// 为指定 HttpClient 应用 leigod 默认请求头。
    /// 方法幂等：多次调用不会重复追加请求头。
    /// </summary>
    private static void ApplyLeigodDefaultHeaders(this HttpClient client)
    {
        var h = client.DefaultRequestHeaders;

        h.UserAgent.Clear();
        h.Accept.Clear();
        h.AcceptEncoding.Clear();
        h.AcceptLanguage.Clear();

        h.UserAgent.TryParseAdd(UserAgent);
        h.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
        h.AcceptEncoding.TryParseAdd("gzip, deflate");
        h.AcceptLanguage.TryParseAdd("zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");

        h.Remove("DNT");
        h.Add("DNT", "1");

        h.Referrer = new Uri("https://www.leigod.com/");

        h.Remove("Sec-Fetch-Dest");
        h.Add("Sec-Fetch-Dest", "empty");
        h.Remove("Sec-Fetch-Mode");
        h.Add("Sec-Fetch-Mode", "cors");
        h.Remove("Sec-Fetch-Site");
        h.Add("Sec-Fetch-Site", "same-site");
    }
}