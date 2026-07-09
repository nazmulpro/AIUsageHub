using System.Net.Http.Headers;

namespace AIUsageHub.Providers.Claude;

/// <summary>
/// Calls Claude's usage API endpoints.
/// Claude exposes usage data through the Anthropic API and
/// through local Claude Desktop session endpoints.
/// </summary>
internal sealed class ClaudeClient : IDisposable
{
    private readonly HttpClient _http;
    private const string AnthropicApiBase = "https://api.anthropic.com/v1";
    private const string AnthropicVersion = "2023-06-01";

    public ClaudeClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Fetches usage data from Claude Desktop's local server (if running).
    /// Claude Desktop runs a local server on port 8321.
    /// </summary>
    public async Task<JsonDocument?> FetchLocalUsageAsync(CancellationToken ct)
    {
        try
        {
            // Claude Desktop local endpoint
            var response = await _http.GetAsync("http://localhost:8321/api/usage", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonDocument.Parse(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudeClient] Local fetch failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Fetches usage from Anthropic's API using the API key.
    /// </summary>
    public async Task<JsonDocument?> FetchApiUsageAsync(string authHeader, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{AnthropicApiBase}/organizations/usage");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            request.Headers.Add("anthropic-version", AnthropicVersion);

            var response = await _requestWithRetry(request, ct);
            if (response != null && response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonDocument.Parse(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudeClient] API fetch failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Fetches current session/token usage for a specific conversation or session.
    /// Uses the /messages/count endpoint.
    /// </summary>
    public async Task<JsonDocument?> FetchSessionUsageAsync(string authHeader, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{AnthropicApiBase}/messages/count");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            request.Headers.Add("anthropic-version", AnthropicVersion);

            var response = await _requestWithRetry(request, ct);
            if (response != null && response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonDocument.Parse(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudeClient] Session fetch failed: {ex.Message}");
        }
        return null;
    }

    private async Task<HttpResponseMessage?> _requestWithRetry(
        HttpRequestMessage request, CancellationToken ct, int maxRetries = 2)
    {
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                var response = await _http.SendAsync(request, ct);
                if ((int)response.StatusCode >= 500 && i < maxRetries)
                {
                    await Task.Delay(1000 * (i + 1), ct);
                    // Clone request for retry
                    var clone = new HttpRequestMessage(request.Method, request.RequestUri!);
                    foreach (var (key, values) in request.Headers)
                        clone.Headers.TryAddWithoutValidation(key, values);
                    request.Dispose();
                    request = clone;
                    continue;
                }
                return response;
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception) when (i < maxRetries)
            {
                await Task.Delay(1000 * (i + 1), ct);
            }
        }
        return null;
    }

    public void Dispose() => _http.Dispose();
}