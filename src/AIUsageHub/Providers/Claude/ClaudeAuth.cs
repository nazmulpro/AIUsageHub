using System.Text.Json;

namespace AIUsageHub.Providers.Claude;

/// <summary>
/// Reads Claude desktop credentials from Windows %APPDATA%\Claude\.
/// Claude Desktop stores its OAuth tokens and session info locally.
/// </summary>
internal static class ClaudeAuth
{
    private static readonly string ClaudeAppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");

    /// <summary>Path to Claude Desktop config</summary>
    private static readonly string ConfigPath =
        Path.Combine(ClaudeAppData, "claude_desktop_config.json");

    /// <summary>Path to Claude session storage</summary>
    private static readonly string SessionDir =
        Path.Combine(ClaudeAppData, "session");

    public static bool CredentialsExist()
    {
        // Check for config file or session directory
        if (File.Exists(ConfigPath)) return true;
        if (Directory.Exists(SessionDir))
        {
            return Directory.EnumerateFiles(SessionDir, "*.json").Any();
        }
        return false;
    }

    public static ClaudeCredentials? LoadCredentials()
    {
        // Try config file first
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var doc = JsonDocument.Parse(json);

                // Claude Desktop config may contain API key or OAuth info
                if (doc.RootElement.TryGetProperty("apiKey", out var apiKey))
                {
                    return new ClaudeCredentials { ApiKey = apiKey.GetString() };
                }

                // Check for OAuth session
                if (doc.RootElement.TryGetProperty("oauthToken", out var oauth))
                {
                    return new ClaudeCredentials { OauthToken = oauth.GetString() };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClaudeAuth] Failed to read config: {ex.Message}");
            }
        }

        // Try session directory
        if (Directory.Exists(SessionDir))
        {
            var files = Directory.GetFiles(SessionDir, "*.json");
            if (files.Length > 0)
            {
                try
                {
                    var json = File.ReadAllText(files[0]);
                    var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("accessToken", out var token))
                    {
                        return new ClaudeCredentials { OauthToken = token.GetString() };
                    }

                    if (doc.RootElement.TryGetProperty("sessionKey", out var key))
                    {
                        return new ClaudeCredentials { SessionKey = key.GetString() };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClaudeAuth] Failed to read session: {ex.Message}");
                }
            }
        }

        return null;
    }
}

internal sealed record ClaudeCredentials
{
    public string? ApiKey { get; init; }
    public string? OauthToken { get; init; }
    public string? SessionKey { get; init; }

    public string GetAuthHeader()
    {
        if (!string.IsNullOrEmpty(ApiKey)) return $"Bearer {ApiKey}";
        if (!string.IsNullOrEmpty(OauthToken)) return $"Bearer {OauthToken}";
        throw new InvalidOperationException("No Claude credentials available");
    }

    public bool IsValid => !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(OauthToken);
}