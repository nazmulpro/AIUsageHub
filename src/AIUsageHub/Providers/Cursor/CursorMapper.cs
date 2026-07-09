using System.Text.Json;
using AIUsageHub.Models;

namespace AIUsageHub.Providers.Cursor;

internal static class CursorMapper
{
    public static ProviderSnapshot MapUsage(JsonDocument doc, string? plan = null)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        // Credits
        if (root.TryGetProperty("credits", out var credits))
        {
            var used = GetDouble(credits, "used");
            var limit = GetDouble(credits, "limit");
            if (limit > 0)
                lines.Add(MetricLine.Progress("Credits", used, limit, MetricFormat.Count));
        }

        // Premium requests
        if (root.TryGetProperty("premium_requests", out var premium))
        {
            var used = GetDouble(premium, "used");
            var limit = GetDouble(premium, "limit");
            if (limit > 0)
                lines.Add(MetricLine.Progress("Premium Requests", used, limit, MetricFormat.Count));
        }

        // Daily spend
        if (root.TryGetProperty("daily_spend", out var daily))
        {
            var amount = GetDouble(daily, "amount_cents") / 100.0;
            var tokens = GetLong(daily, "input_tokens") + GetLong(daily, "output_tokens");
            lines.Add(MetricLine.Values("Today",
                new MetricValue(amount, MetricValueKind.Dollars),
                new MetricValue(tokens, MetricValueKind.Tokens, "tokens")));
        }

        // Total usage
        if (root.TryGetProperty("total_usage", out var total))
        {
            var autoRequests = GetLong(total, "auto_requests");
            var apiRequests = GetLong(total, "api_requests");
            lines.Add(MetricLine.Values("Total Usage",
                new MetricValue(autoRequests, MetricValueKind.Count, "auto"),
                new MetricValue(apiRequests, MetricValueKind.Count, "api")));
        }

        return ProviderSnapshot.Success("Cursor", lines.ToArray(), plan);
    }

    private static double GetDouble(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    }
    private static long GetLong(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
    }
}