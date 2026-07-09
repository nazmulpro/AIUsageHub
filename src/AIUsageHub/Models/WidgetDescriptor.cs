namespace AIUsageHub.Models;

public record WidgetDescriptor
{
    public string ProviderName { get; init; } = string.Empty;
    public string MetricLabel { get; init; } = string.Empty;
    public bool IsPinnedToTray { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool IsAlwaysVisible { get; init; } = true;  // above the "On Demand" line
    public int SortOrder { get; init; }
}