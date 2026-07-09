namespace AIUsageHub.Models;

public enum MetricValueKind
{
    Dollars,
    Tokens,
    Count
}

public record MetricValue(
    double Value,
    MetricValueKind Kind,
    string? Unit = null   // e.g. "tokens", "requests"
);