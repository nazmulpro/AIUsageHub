using System.Text.Json;
using AIUsageHub.Models;

namespace AIUsageHub.Providers.Claude;

/// <summary>
/// Maps Claude API responses into normalized MetricLine values.
/// Produces: session usage, weekly usage, plan badge, daily spend.
/// </summary>
internal static class ClaudeMapper
{
    public static ProviderSnapshot MapLocalUsage(JsonDocument doc, string? plan = null)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        // Session limit
        if (root.TryGetProperty("session", out var session))
        {
            var used = GetDouble(session, "used");
            var limit = GetDouble(session, "limit");
            var resetsAt = GetTimestamp(session, "resets_at");
            var periodMs = GetLong(session, "period_duration_ms");

            if (limit > 0)
                lines.Add(MetricLine.Progress("Session", used, limit, MetricFormat.Percent, resetsAt, periodMs));
        }

        // Weekly limit
        if (root.TryGetProperty("weekly", out var weekly))
        {
            var used = GetDouble(weekly, "used");
            var limit = GetDouble(weekly, "limit");
            var resetsAt = GetTimestamp(weekly, "resets_at");
            var periodMs = GetLong(weekly, "period_duration_ms");

            if (limit > 0)
                lines.Add(MetricLine.Progress("Weekly", used, limit, MetricFormat.Percent, resetsAt, periodMs));
        }

        // Sonnet usage (Claude Pro/Team plans)
        if (root.TryGetProperty("sonnet", out var sonnet))
        {
            var used = GetDouble(sonnet, "used");
            var limit = GetDouble(sonnet, "limit");

            if (limit > 0)
                lines.Add(MetricLine.Progress("Sonnet", used, limit, MetricFormat.Percent));
        }

        // Extra usage balance (pay-as-you-go overflow)
        if (root.TryGetProperty("extra_usage", out var extra))
        {
            var balance = GetDouble(extra, "balance_cents") / 100.0;
            lines.Add(MetricLine.Values("Extra Usage", new MetricValue(balance, MetricValueKind.Dollars, "remaining")));
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

        // Plan name
        if (root.TryGetProperty("plan", out var planElem))
            plan ??= planElem.GetString();

        if (plan != null)
            lines.Add(MetricLine.Badge(plan, plan));

        return ProviderSnapshot.Success("Claude", lines.ToArray(), plan);
    }

    public static ProviderSnapshot MapApiUsage(JsonDocument doc)
    {
        var root = doc.RootElement;
        var lines = new List<MetricLine>();

        if (root.TryGetProperty("data", out var data))
        {
            foreach (var item in data.EnumerateArray())
            {
                var type = item.GetProperty("type").GetString() ?? "unknown";
                var inputTokens = GetLong(item, "input_tokens");
                var outputTokens = GetLong(item, "output_tokens");

                lines.Add(MetricLine.Values(
                    char.ToUpper(type[0]) + type[1..],
                    new MetricValue(inputTokens, MetricValueKind.Tokens, "input"),
                    new MetricValue(outputTokens, MetricValueKind.Tokens, "output")));
            }
        }

        return ProviderSnapshot.Success("Claude", lines.ToArray());
    }

    private static double GetDouble(JsonElement elem, string prop)
    {
        if (!elem.TryGetProperty(prop, out var val)) return 0;
        return val.ValueKind switch
        {
            JsonValueKind.Number => val.GetDouble(),
            JsonValueKind.String => double.TryParse(val.GetString(), out var d) ? d : 0,
            _ => 0
        };
    }

    private static long GetLong(JsonElement elem, string prop)
    {
        if (!elem.TryGetProperty(prop, out var val)) return 0;
        return val.ValueKind switch
        {
            JsonValueKind.Number => val.GetInt64(),
            JsonValueKind.String => long.TryParse(val.GetString(), out var l) ? l : 0,
            _ => 0
        };
    }

    private static DateTime? GetTimestamp(JsonElement elem, string prop)
    {
        if (!elem.TryGetProperty(prop, out var val)) return null;
        var s = val.GetString();
        if (s == null) return null;
        if (double.TryParse(s, out var epoch))
            return DateTimeOffset.FromUnixTimeSeconds((long)epoch).DateTime;
        if (DateTimeOffset.TryParse(s, out var dto))
            return dto.UtcDateTime;
        return null;
    }
}