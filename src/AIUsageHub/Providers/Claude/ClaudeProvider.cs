using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Providers.Claude;

/// <summary>
/// Claude provider: reads local Claude Desktop credentials,
/// calls Anthropic API or local server, maps to normalized metrics.
/// Pipeline: ClaudeAuth -> ClaudeClient -> ClaudeMapper
/// </summary>
public sealed class ClaudeProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private readonly ConfigManager? _config;

    public ClaudeProvider() : this(new HttpClient()) { }

    public ClaudeProvider(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public ClaudeProvider(HttpClient http, ConfigManager config) : this(http) => _config = config;

    public string Name => "Claude";
    public string BrandColor => "#D97757"; // Terracotta (matches macOS original)
    public bool HasLocalCredentials() => ClaudeAuth.CredentialsExist() || !string.IsNullOrEmpty(GetApiKey());

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var creds = ClaudeAuth.LoadCredentials();
        var apiKey = GetApiKey();
        if ((creds == null || !creds.IsValid) && string.IsNullOrEmpty(apiKey))
            return ProviderSnapshot.Failed(Name, "Not logged in");

        using var client = new ClaudeClient(_http);

        // Strategy 1: Try Claude Desktop local server (no auth needed)
        var localDoc = await client.FetchLocalUsageAsync(ct);
        if (localDoc != null)
            return ClaudeMapper.MapLocalUsage(localDoc);

        // Auth from configured API key, else local creds
        var authHeader = !string.IsNullOrEmpty(apiKey) ? $"Bearer {apiKey}" : creds!.GetAuthHeader();

        // Strategy 2: Try Anthropic API
        var apiDoc = await client.FetchApiUsageAsync(authHeader, ct);
        if (apiDoc != null)
            return ClaudeMapper.MapApiUsage(apiDoc);

        // Strategy 3: Try session usage endpoint (local creds only)
        if (creds != null && creds.IsValid)
        {
            var sessionDoc = await client.FetchSessionUsageAsync(creds.GetAuthHeader(), ct);
            if (sessionDoc != null)
                return ClaudeMapper.MapLocalUsage(sessionDoc);
        }

        return ProviderSnapshot.Failed(Name, "Could not reach Claude API or local server");
    }

    private string? GetApiKey() => _config != null && _config.ApiKeys.TryGetValue("Claude", out var k) ? k : null;
}