using System.Net.Http.Headers;
using System.Text.Json;

namespace Pupstrap.Core;

public static class Http
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var handler = new HttpClientHandler { UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Pupstrap", App.Version));
        return client;
    }

    public static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken token = default)
    {
        using var response = await Client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }

    public static async Task<string> GetStringAsync(string url, CancellationToken token = default)
    {
        using var response = await Client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token);
    }
}
