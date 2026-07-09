namespace AIUsageHub.Models;

public record ProviderSnapshot
{
    public string ProviderName { get; init; } = string.Empty;
    public MetricLine[] Lines { get; init; } = Array.Empty<MetricLine>();
    public string? Plan { get; init; }
    public string? Error { get; init; }
    public DateTime RefreshedAt { get; init; } = DateTime.UtcNow;

    public static ProviderSnapshot Success(string name, MetricLine[] lines, string? plan = null)
        => new() { ProviderName = name, Lines = lines, Plan = plan };

    public static ProviderSnapshot Failed(string name, string error)
        => new() { ProviderName = name, Error = error, Lines = new[] { MetricLine.Error("Error", error) } };
}