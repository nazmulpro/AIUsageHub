namespace AIUsageHub.Services;

public sealed class HttpService
{
    private readonly ConfigManager _config;
    private HttpClient? _client;

    public HttpService(ConfigManager config)
    {
        _config = config;
    }

    public HttpClient GetClient()
    {
        if (_client != null) return _client;
        _client = CreateClient();
        return _client;
    }

    public void RefreshClient()
    {
        _client?.Dispose();
        _client = CreateClient();
    }

    private HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.Add("User-Agent", "AIUsageHub/1.0 (Windows)");
        return client;
    }
}