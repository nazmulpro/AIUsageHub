using AIUsageHub.Models;

namespace AIUsageHub.Providers.Cursor;

public sealed class CursorProvider : IProviderRuntime
{
    private readonly HttpClient _http;
    public CursorProvider() : this(new HttpClient()) { }
    public CursorProvider(HttpClient http) { _http = http; _http.Timeout = TimeSpan.FromSeconds(15); }
    public string Name => "Cursor";
    public string BrandColor => "#000000"; // Cursor black
    public bool HasLocalCredentials() => CursorAuth.CredentialsExist();

    public async Task<ProviderSnapshot> RefreshAsync(CancellationToken ct)
    {
        var creds = CursorAuth.LoadCredentials();
        if (creds == null || !creds.IsValid)
            return ProviderSnapshot.Failed(Name, "Not logged in");

        using var client = new CursorClient(_http);
        var doc = await client.FetchUsageAsync(creds.GetAuthHeader(), ct);
        if (doc != null)
            return CursorMapper.MapUsage(doc);

        return ProviderSnapshot.Failed(Name, "Could not reach Cursor API");
    }
}