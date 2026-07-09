using System.Text.Json;
using System.Net.Http.Headers;
using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Providers.OpenRouter;

public sealed class OpenRouterProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private readonly ConfigManager _config;
    private const string ApiBase = "https://openrouter.ai/api/v1";

    public OpenRouterProvider(HttpClient http, ConfigManager config)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
        _config = config;
    }

    public string Name => "OpenRouter";
    public string BrandColor => "#6D28D9"; // Purple
    public bool HasLocalCredentials() => !string.IsNullOrEmpty(GetApiKey());

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return ProviderSnapshot.Failed(Name, "No API key configured");

        try
        {
            // /credits is the authoritative source for balance + lifetime spend.
            using var creditsReq = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/credits");
            creditsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var creditsResp = await _http.SendAsync(creditsReq, ct);
            if (!creditsResp.IsSuccessStatusCode)
                return ProviderSnapshot.Failed(Name, $"API returned {(int)creditsResp.StatusCode}");
            var creditsBody = await creditsResp.Content.ReadAsStringAsync(ct);
            var creditsDoc = JsonDocument.Parse(creditsBody);

            // /key is best-effort: tier, daily/weekly/monthly spend, optional per-key cap.
            JsonDocument? keyDoc = null;
            try
            {
                using var keyReq = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/key");
                keyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var keyResp = await _http.SendAsync(keyReq, ct);
                if (keyResp.IsSuccessStatusCode)
                    keyDoc = JsonDocument.Parse(await keyResp.Content.ReadAsStringAsync(ct));
            }
            catch { /* best-effort: balance still renders from /credits */ }

            return MapUsage(creditsDoc, keyDoc);
        }
        catch (Exception ex)
        {
            return ProviderSnapshot.Failed(Name, ex.Message);
        }
    }

    private ProviderSnapshot MapUsage(JsonDocument creditsDoc, JsonDocument? keyDoc)
    {
        var lines = new List<MetricLine>();
        string? plan = null;

        // --- /credits: total_credits + total_usage ---
        if (creditsDoc.RootElement.TryGetProperty("data", out var cData))
        {
            var totalCredits = GetNumber(cData, "total_credits");
            var totalUsage = GetNumber(cData, "total_usage");

            if (totalCredits > 0)
            {
                lines.Add(MetricLine.Progress("Credits", totalUsage, totalCredits, MetricFormat.Dollars));
                var balance = Math.Max(0, totalCredits - totalUsage);
                lines.Add(MetricLine.Values("Balance", new MetricValue(balance, MetricValueKind.Dollars)));
            }

            // Lifetime spend is authoritative here — always shown.
            lines.Add(MetricLine.Values("Total Usage", new MetricValue(totalUsage, MetricValueKind.Dollars)));
        }

        // --- /key: tier + period spends + optional per-key cap ---
        if (keyDoc != null && keyDoc.RootElement.TryGetProperty("data", out var kData))
        {
            var daily = GetNumber(kData, "usage_daily");
            var weekly = GetNumber(kData, "usage_weekly");
            var monthly = GetNumber(kData, "usage_monthly");
            var keyLimit = GetNullableNumber(kData, "limit");
            var isFreeTier = kData.TryGetProperty("is_free_tier", out var ft)
                             && ft.ValueKind == JsonValueKind.True;

            lines.Add(MetricLine.Values("Today", new MetricValue(daily, MetricValueKind.Dollars)));
            lines.Add(MetricLine.Values("This Week", new MetricValue(weekly, MetricValueKind.Dollars)));
            lines.Add(MetricLine.Values("This Month", new MetricValue(monthly, MetricValueKind.Dollars)));

            // Key cap is optional; only shown when the key has one configured.
            if (keyLimit.HasValue && keyLimit.Value > 0)
            {
                var remaining = GetNullableNumber(kData, "limit_remaining") ?? 0;
                var used = Math.Max(0, keyLimit.Value - remaining);
                lines.Add(MetricLine.Progress("Key Limit", used, keyLimit.Value, MetricFormat.Dollars));
            }

            plan = isFreeTier ? "Free tier" : "Pay as you go";
        }

        return lines.Count > 0
            ? ProviderSnapshot.Success("OpenRouter", lines.ToArray(), plan)
            : ProviderSnapshot.Success("OpenRouter",
                new[] { MetricLine.Text("Status", "Connected - no usage yet") }, plan);
    }

    private static double GetNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var val)) return 0;
        return val.ValueKind == JsonValueKind.Number ? val.GetDouble() : 0;
    }

    private static double? GetNullableNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var val)) return null;
        return val.ValueKind == JsonValueKind.Number ? val.GetDouble() : (double?)null;
    }

    private string? GetApiKey()
    {
        _config.ApiKeys.TryGetValue("OpenRouter", out var key);
        return key;
    }
}