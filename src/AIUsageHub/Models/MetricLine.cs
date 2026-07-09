namespace AIUsageHub.Models;

public record MetricLine
{
    public MetricKind Kind { get; init; }
    public string Label { get; init; } = string.Empty;
    public double Used { get; init; }
    public double Limit { get; init; }
    public MetricFormat Format { get; init; } = MetricFormat.Percent;
    public DateTime? ResetsAt { get; init; }
    public long PeriodDurationMs { get; init; }
    public MetricValue[]? MetricValues { get; init; }
    public string? TextValue { get; init; }
    public string? BadgeText { get; init; }
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    // Factory methods
    public static MetricLine Progress(string label, double used, double limit, MetricFormat format,
        DateTime? resetsAt = null, long periodDurationMs = 0)
        => new() { Kind = MetricKind.Progress, Label = label, Used = used, Limit = limit, Format = format, ResetsAt = resetsAt, PeriodDurationMs = periodDurationMs };

    public static MetricLine Values(string label, params MetricValue[] values)
        => new() { Kind = MetricKind.Values, Label = label, MetricValues = values };

    public static MetricLine Text(string label, string text)
        => new() { Kind = MetricKind.Text, Label = label, TextValue = text };

    public static MetricLine Badge(string label, string badgeText)
        => new() { Kind = MetricKind.Badge, Label = label, BadgeText = badgeText };

    public static MetricLine Error(string label, string message)
        => new() { Kind = MetricKind.Text, Label = label, TextValue = message, IsError = true, ErrorMessage = message };
}