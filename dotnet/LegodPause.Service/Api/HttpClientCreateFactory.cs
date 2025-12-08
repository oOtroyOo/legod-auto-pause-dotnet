using System.Net.Http;

namespace LegodPause.Service.Api;

public class HttpClientCreateFactory
{
    private static readonly Lazy<HttpClient> _lazyClient = new Lazy<HttpClient>(() => new HttpClient());

    public static HttpClient Create()
    {
        HttpClient client = null;

        if (Program.AppHost != null)
        {
            var httpClient = Program.AppHost.Services.GetService<HttpClient>();
            if (httpClient != null)
            {
                client = httpClient;
            }
        }

        client ??= _lazyClient.Value;

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63");
        return client;
    }
}