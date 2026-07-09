using System.Net.Http.Headers;
using System.Text.Json;
using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Providers.Zai;

public sealed class ZaiProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private readonly ConfigManager _config;

    // Z.ai's subscription UI uses these internal endpoints (not the /v1 chat API).
    private const string ApiBase = "https://api.z.ai/api";

    private const long MsHour = 3_600_000L;
    private const long MsDay = 24L * MsHour;

    public ZaiProvider(HttpClient http, ConfigManager config)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
        _config = config;
    }

    public string Name => "Z.ai";
    public string BrandColor => "#3B82F6"; // Blue
    public bool HasLocalCredentials() => !string.IsNullOrEmpty(GetApiKey());

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return ProviderSnapshot.Failed(Name, "No Z.ai API key configured");

        try
        {
            // Quota meters — the required call.
            var quota = await FetchAsync($"{ApiBase}/monitor/usage/quota/limit", apiKey, ct);

            // Plan name — best-effort; a failure here must not blank the meters.
            string? plan = null;
            try
            {
                var sub = await FetchAsync($"{ApiBase}/biz/subscription/list", apiKey, ct);
                plan = ExtractPlanName(sub);
            }
            catch { /* best-effort */ }

            return MapUsage(quota, plan);
        }
        catch (UnauthorizedAccessException)
        {
            return ProviderSnapshot.Failed(Name, "Z.ai API key invalid");
        }
        catch (Exception ex)
        {
            return ProviderSnapshot.Failed(Name, ex.Message);
        }
    }

    private async Task<JsonDocument> FetchAsync(string url, string apiKey, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("invalid key");
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Z.ai API returned {(int)resp.StatusCode}");
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    private static ProviderSnapshot MapUsage(JsonDocument quotaDoc, string? plan)
    {
        var tokenLines = new List<MetricLine>();
        var countLines = new List<MetricLine>();

        if (quotaDoc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("limits", out var limits)
            && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in limits.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                var resetsAt = GetResetTime(item);

                if (type == "TOKENS_LIMIT")
                {
                    var label = WindowMilliseconds(item) < MsDay ? "Session" : "Weekly";
                    var used = GetNumber(item, "percentage");
                    tokenLines.Add(MetricLine.Progress(label, used, 100, MetricFormat.Percent, resetsAt));
                }
                else if (type == "TIME_LIMIT")
                {
                    var used = GetNumber(item, "currentValue");
                    var remaining = GetNumber(item, "remaining");
                    var limit = used + remaining > 0 ? used + remaining : GetNumber(item, "usage");
                    countLines.Add(MetricLine.Progress("Web Searches", used, limit, MetricFormat.Count, resetsAt));
                }
            }
        }

        var lines = new List<MetricLine>();
        lines.AddRange(tokenLines);
        lines.AddRange(countLines);

        return lines.Count > 0
            ? ProviderSnapshot.Success("Z.ai", lines.ToArray(), plan)
            : ProviderSnapshot.Success("Z.ai",
                new[] { MetricLine.Text("Status", "No usage data") }, plan);
    }

    private static string? ExtractPlanName(JsonDocument subDoc)
    {
        if (!subDoc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            return null;

        string? first = null;
        foreach (var item in data.EnumerateArray())
        {
            var name = item.TryGetProperty("productName", out var pn) ? pn.GetString() : null;
            if (string.IsNullOrEmpty(name)) continue;
            first ??= name;

            var inPeriod = item.TryGetProperty("inCurrentPeriod", out var ip)
                           && ip.ValueKind == JsonValueKind.True;
            var status = item.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (inPeriod && status == "VALID")
                return name;
        }
        return first;
    }

    private static long WindowMilliseconds(JsonElement item)
    {
        var unit = GetLong(item, "unit");
        var number = GetLong(item, "number");
        if (number <= 0) number = 1;
        var perUnit = unit switch
        {
            3 => MsHour,          // HOUR (e.g. 5h session)
            4 => MsDay,           // DAY  (e.g. 7d weekly)
            5 => 30L * MsDay,     // MONTH (monthly web-search quota)
            _ => MsHour           // unknown unit → assume sub-daily
        };
        return perUnit * number;
    }

    private static DateTime? GetResetTime(JsonElement item)
    {
        if (!item.TryGetProperty("nextResetTime", out var v) || v.ValueKind != JsonValueKind.Number)
            return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(v.GetInt64()).UtcDateTime;
    }

    private static double GetNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var val)) return 0;
        return val.ValueKind == JsonValueKind.Number ? val.GetDouble() : 0;
    }

    private static long GetLong(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var val)) return 0;
        return val.ValueKind == JsonValueKind.Number ? val.GetInt64() : 0;
    }

    private string? GetApiKey()
    {
        _config.ApiKeys.TryGetValue("Z.ai", out var key);
        return key;
    }
}
