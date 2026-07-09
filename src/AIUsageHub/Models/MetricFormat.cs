namespace AIUsageHub.Models;

public enum MetricFormat
{
    Percent,       // Quota-style (session, weekly)
    Dollars,       // Capped dollar amount (credits with ceiling)
    Count,         // Capped count (requests per cycle)
    Text           // Plain text display
}