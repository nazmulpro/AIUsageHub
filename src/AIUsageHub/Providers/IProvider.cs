using AIUsageHub.Models;

namespace AIUsageHub.Providers;

public interface IProviderRuntime
{
    /// Display name shown in the UI (e.g. "Claude", "Cursor")
    string Name { get; }

    /// Brand color for the provider (hex, e.g. "#D97757" for Claude terracotta)
    string BrandColor { get; }

    /// Quick check if the user has this provider's credentials locally (no network).
    /// Used for auto-detection on first run.
    bool HasLocalCredentials();

    /// Fetch current usage from the provider's API. Returns a normalized snapshot.
    Task<ProviderSnapshot> RefreshAsync(CancellationToken ct);
}