using System.Net.Http.Headers;
using System.Text.Json;

namespace AIUsageHub.Providers.Codex;

internal sealed class CodexClient : IDisposable
{
    private readonly HttpClient _http;
    private const string ApiBase = "https://api.openai.com/v1";

    public CodexClient(HttpClient http) => _http = http;

    public async Task<JsonDocument?> FetchUsageAsync(string authHeader, CancellationToken ct)
    {
        try
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/usage?date={date}");
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
            System.Diagnostics.Debug.WriteLine($"[CodexClient] Fetch failed: {ex.Message}");
        }
        return null;
    }

    public void Dispose() { }
}