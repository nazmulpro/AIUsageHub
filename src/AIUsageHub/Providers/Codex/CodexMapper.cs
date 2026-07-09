using System.Text.Json;
using AIUsageHub.Models;

namespace AIUsageHub.Providers.Codex;

internal static class CodexMapper
{
    public static ProviderSnapshot MapUsage(JsonDocument doc, string? plan = null)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        // Session usage
        if (root.TryGetProperty("session", out var session))
        {
            var used = GetDouble(session, "used");
            var limit = GetDouble(session, "limit");
            var resetsAt = GetTimestamp(session, "resets_at");
            var periodMs = GetLong(session, "period_duration_ms");
            if (limit > 0)
                lines.Add(MetricLine.Progress("Session", used, limit, MetricFormat.Percent, resetsAt, periodMs));
        }

        // Weekly usage
        if (root.TryGetProperty("weekly", out var weekly))
        {
            var used = GetDouble(weekly, "used");
            var limit = GetDouble(weekly, "limit");
            var resetsAt = GetTimestamp(weekly, "resets_at");
            var periodMs = GetLong(weekly, "period_duration_ms");
            if (limit > 0)
                lines.Add(MetricLine.Progress("Weekly", used, limit, MetricFormat.Percent, resetsAt, periodMs));
        }

        // Credits
        if (root.TryGetProperty("credits", out var credits))
        {
            var used = GetDouble(credits, "used_cents") / 100.0;
            var limit = GetDouble(credits, "limit_cents") / 100.0;
            if (limit > 0)
                lines.Add(MetricLine.Progress("Credits", used, limit, MetricFormat.Dollars));
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

        return ProviderSnapshot.Success("Codex", lines.ToArray(), plan);
    }

    /// Maps OpenAI's legacy daily usage response (GET /v1/usage?date=...), which is the
    /// only usage endpoint reachable with a standard (non-admin) project API key.
    public static ProviderSnapshot MapLegacyDaily(JsonDocument doc)
    {
        var root = doc.RootElement;
        double cost = 0;
        double tokens = 0;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                cost += GetDouble(item, "cost");
                tokens += GetDouble(item, "n_context_tokens_total") + GetDouble(item, "n_generated_tokens_total");
            }
        }

        var lines = new List<MetricLine>();
        if (cost > 0 || tokens > 0)
        {
            lines.Add(MetricLine.Values("Today",
                new MetricValue(cost, MetricValueKind.Dollars),
                new MetricValue((long)tokens, MetricValueKind.Tokens, "tokens")));
        }
        else
        {
            lines.Add(MetricLine.Text("Status", "Connected - no usage today"));
        }

        return ProviderSnapshot.Success("Codex", lines.ToArray());
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
    private static DateTime? GetTimestamp(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return null;
        var s = v.GetString();
        if (s == null) return null;
        if (double.TryParse(s, out var epoch))
            return DateTimeOffset.FromUnixTimeSeconds((long)epoch).DateTime;
        if (DateTimeOffset.TryParse(s, out var dto))
            return dto.UtcDateTime;
        return null;
    }
}