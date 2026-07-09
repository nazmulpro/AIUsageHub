using AIUsageHub.Models;
using AIUsageHub.Services;

namespace AIUsageHub.Providers.Codex;

public sealed class CodexProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    private readonly ConfigManager? _config;
    public CodexProvider() : this(new HttpClient()) { }
    public CodexProvider(HttpClient http) { _http = http; _http.Timeout = TimeSpan.FromSeconds(15); }
    public CodexProvider(HttpClient http, ConfigManager config) : this(http) => _config = config;
    public string Name => "Codex";
    public string BrandColor => "#10A37F"; // OpenAI green
    public bool HasLocalCredentials() => CodexAuth.CredentialsExist() || !string.IsNullOrEmpty(GetApiKey());

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var creds = CodexAuth.LoadCredentials();
        var apiKey = GetApiKey();

        string? authHeader = creds != null && creds.IsValid
            ? creds.GetAuthHeader()
            : (!string.IsNullOrEmpty(apiKey) ? $"Bearer {apiKey}" : null);

        if (authHeader == null)
            return ProviderSnapshot.Failed(Name, "Not logged in");

        using var client = new CodexClient(_http);
        var doc = await client.FetchUsageAsync(authHeader, ct);
        if (doc != null)
            return CodexMapper.MapLegacyDaily(doc);

        return ProviderSnapshot.Failed(Name, "Could not reach Codex/OpenAI API");
    }

    private string? GetApiKey() => _config != null && _config.ApiKeys.TryGetValue("Codex", out var k) ? k : null;
}