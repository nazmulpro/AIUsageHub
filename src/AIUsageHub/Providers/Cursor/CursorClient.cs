using System.Net.Http.Headers;
using System.Text.Json;

namespace AIUsageHub.Providers.Cursor;

internal sealed class CursorClient : IDisposable
{
    private readonly HttpClient _http;
    private const string ApiBase = "https://api2.cursor.sh";

    public CursorClient(HttpClient http) => _http = http;

    public async Task<JsonDocument?> FetchUsageAsync(string authHeader, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/usage");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonDocument.Parse(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CursorClient] Fetch failed: {ex.Message}");
        }
        return null;
    }

    public void Dispose() { }
}