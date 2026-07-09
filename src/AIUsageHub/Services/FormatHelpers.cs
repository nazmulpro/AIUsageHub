namespace AIUsageHub.Services;

public static class FormatHelpers
{
    public static string FormatDollars(double amount)
    {
        if (amount >= 1_000_000) return $"${amount / 1_000_000:F1}M";
        if (amount >= 1_000) return $"${amount / 1_000:F1}K";
        return $"${amount:F2}";
    }

    public static string FormatPercent(double used, double limit)
    {
        if (limit <= 0) return "0%";
        var remaining = Math.Max(0, 100 - (used / limit * 100));
        return $"{remaining:F0}% left";
    }

    public static string FormatTokens(double tokens)
    {
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000:F1}M tokens";
        if (tokens >= 1_000) return $"{tokens / 1_000:F1}K tokens";
        return $"{tokens:F0} tokens";
    }

    public static string FormatCountdown(DateTime? resetsAt)
    {
        if (resetsAt == null) return string.Empty;
        var remaining = resetsAt.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "Resetting...";

        if (remaining.TotalDays >= 1)
        {
            var days = (int)remaining.TotalDays;
            var hours = remaining.Hours;
            return $"Resets in {days}d {hours}h";
        }
        if (remaining.TotalHours >= 1)
            return $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"Resets in {remaining.Minutes}m";
    }

    public static string FormatPaceIndicator(double used, double limit, DateTime? resetsAt, long periodDurationMs)
    {
        if (resetsAt == null || periodDurationMs <= 0 || limit <= 0) return string.Empty;

        var remaining = resetsAt.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return string.Empty;

        var elapsedRatio = 1 - (remaining.TotalMilliseconds / periodDurationMs);
        var usedRatio = used / limit;
        var projectedFinal = usedRatio / Math.Max(elapsedRatio, 0.001);

        var projectedRemaining = Math.Max(0, 100 - projectedFinal * 100);
        return $"~{projectedRemaining:F0}% left at reset";
    }

    /// Returns "blue", "yellow", or "red" based on burn rate and remaining
    public static string GetBarVerdict(double used, double limit, DateTime? resetsAt, long periodDurationMs)
    {
        if (resetsAt == null || periodDurationMs <= 0 || limit <= 0)
        {
            // No reset window — color by level
            var ratio = used / limit;
            if (ratio >= 0.9) return "red";
            if (ratio >= 0.7) return "yellow";
            return "blue";
        }

        var remaining = resetsAt.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "red";

        var elapsedRatio = 1 - (remaining.TotalMilliseconds / periodDurationMs);
        var usedRatio = used / limit;
        var projectedFinal = usedRatio / Math.Max(elapsedRatio, 0.001);

        // Blue: on course with 10%+ to spare
        if (projectedFinal <= 0.9) return "blue";
        // Yellow: projected 10-0% spare
        if (projectedFinal <= 1.0) return "yellow";
        // Red: will run out or hit exactly at limit
        return "red";
    }
}