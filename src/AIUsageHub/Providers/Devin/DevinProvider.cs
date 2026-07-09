using System.Text.Json;
using System.Net.Http.Headers;
using AIUsageHub.Models;

namespace AIUsageHub.Providers.Devin;

public sealed class DevinProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Devin");

    public DevinProvider() : this(new HttpClient()) { }
    public DevinProvider(HttpClient http) { _http = http; _http.Timeout = TimeSpan.FromSeconds(15); }
    public string Name => "Devin";
    public string BrandColor => "#6366F1"; // Indigo
    public bool HasLocalCredentials()
    {
        if (!Directory.Exists(AppDataDir)) return false;
        return Directory.EnumerateFiles(AppDataDir, "auth*.json").Any();
    }

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var authFile = Path.Combine(AppDataDir, "auth.json");
        if (!File.Exists(authFile))
            return ProviderSnapshot.Failed(Name, "Not logged in");

        try
        {
            var json = File.ReadAllText(authFile);
            var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(token))
                return ProviderSnapshot.Failed(Name, "No auth token found");

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.devin.ai/v1/usage");
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

    private static ProviderSnapshot MapUsage(JsonDocument doc)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        if (root.TryGetProperty("weekly", out var weekly))
        {
            var used = weekly.TryGetProperty("used", out var u) ? u.GetInt64() : 0L;
            var limit = weekly.TryGetProperty("limit", out var l) ? l.GetInt64() : 0L;
            var resetsAt = weekly.TryGetProperty("resets_at", out var r) ? r.GetString() : null;
            DateTime? reset = null;
            if (resetsAt != null && DateTimeOffset.TryParse(resetsAt, out var dto)) reset = dto.UtcDateTime;
            if (limit > 0)
                lines.Add(MetricLine.Progress("Weekly", used, limit, MetricFormat.Count, reset));
        }

        if (root.TryGetProperty("daily", out var daily))
        {
            var used = daily.TryGetProperty("used", out var u) ? u.GetInt64() : 0L;
            var limit = daily.TryGetProperty("limit", out var l) ? l.GetInt64() : 0L;
            var resetsAt = daily.TryGetProperty("resets_at", out var r) ? r.GetString() : null;
            DateTime? reset = null;
            if (resetsAt != null && DateTimeOffset.TryParse(resetsAt, out var dto)) reset = dto.UtcDateTime;
            if (limit > 0)
                lines.Add(MetricLine.Progress("Daily", used, limit, MetricFormat.Count, reset));
        }

        if (root.TryGetProperty("extra_usage", out var extra))
        {
            var balance = extra.TryGetProperty("balance", out var b) ? b.GetDouble() : 0;
            lines.Add(MetricLine.Values("Extra Usage", new MetricValue(balance, MetricValueKind.Count, "remaining")));
        }

        return ProviderSnapshot.Success("Devin", lines.ToArray());
    }
}