using System.Text.Json;
using System.Net.Http.Headers;
using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Providers.Grok;

public sealed class GrokProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private readonly ConfigManager? _config;
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grok");

    public GrokProvider() : this(new HttpClient()) { }
    public GrokProvider(HttpClient http) { _http = http; _http.Timeout = TimeSpan.FromSeconds(15); }
    public GrokProvider(HttpClient http, ConfigManager config) : this(http) => _config = config;
    public string Name => "Grok";
    public string BrandColor => "#1DA1F2"; // Twitter/X blue
    public bool HasLocalCredentials()
        => (!string.IsNullOrEmpty(GetApiKey()))
           || (Directory.Exists(AppDataDir) && Directory.EnumerateFiles(AppDataDir, "config*.json").Any());

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        // Prefer configured API key, fall back to local desktop token
        var token = GetApiKey();
        if (string.IsNullOrEmpty(token))
        {
            var configFile = Path.Combine(AppDataDir, "config.json");
            if (File.Exists(configFile))
            {
                try
                {
                    var json = File.ReadAllText(configFile);
                    var doc = JsonDocument.Parse(json);
                    token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
                }
                catch { /* ignore malformed config */ }
            }
        }

        if (string.IsNullOrEmpty(token))
            return ProviderSnapshot.Failed(Name, "Not logged in");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.x.ai/v1/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var usageDoc = JsonDocument.Parse(body);
                return MapUsage(usageDoc);
            }
            return ProviderSnapshot.Failed(Name, $"API returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ProviderSnapshot.Failed(Name, ex.Message);
        }
    }

    private string? GetApiKey() => _config != null && _config.ApiKeys.TryGetValue("Grok", out var k) ? k : null;

    private static ProviderSnapshot MapUsage(JsonDocument doc)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        if (root.TryGetProperty("credits_used", out var used))
        {
            var usedVal = used.ValueKind == JsonValueKind.Number ? used.GetDouble() : 0;
            var limit = root.TryGetProperty("credits_limit", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetDouble() : 0;
            if (limit > 0)
                lines.Add(MetricLine.Progress("Credits", usedVal, limit, MetricFormat.Count));
            else
                lines.Add(MetricLine.Values("Credits Used", new MetricValue(usedVal, MetricValueKind.Count)));
        }

        if (root.TryGetProperty("payg_spend", out var payg))
        {
            var amount = payg.ValueKind == JsonValueKind.Number ? payg.GetDouble() : 0;
            lines.Add(MetricLine.Values("Pay-as-you-go", new MetricValue(amount, MetricValueKind.Dollars)));
        }

        return ProviderSnapshot.Success("Grok", lines.ToArray());
    }
}