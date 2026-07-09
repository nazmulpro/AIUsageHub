namespace AIUsageHub.Models;

public enum MetricKind
{
    Progress,  // Bounded meter with used/limit (session, weekly, credits)
    Values,    // Unbounded numeric row (spend in dollars + tokens)
    Text,      // Displayed as-is string
    Badge      // Short status pill (e.g. "Disabled", "Pay-as-you-go")
}