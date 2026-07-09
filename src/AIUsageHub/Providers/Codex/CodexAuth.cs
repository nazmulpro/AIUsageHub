using System.Text.Json;

namespace AIUsageHub.Providers.Codex;

internal static class CodexAuth
{
    private static readonly string[] SearchPaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "codex"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenAI"),
    };

    public static bool CredentialsExist()
    {
        return SearchPaths.Any(dir =>
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.EnumerateFiles(dir, "*.json").Any();
        });
    }

    public static CodexCredentials? LoadCredentials()
    {
        foreach (var dir in SearchPaths)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("api_key", out var apiKey) ||
                        root.TryGetProperty("apiKey", out apiKey))
                    {
                        return new CodexCredentials { ApiKey = apiKey.GetString() };
                    }
                    if (root.TryGetProperty("token", out var token))
                    {
                        return new CodexCredentials { ApiKey = token.GetString() };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CodexAuth] Failed to read {file}: {ex.Message}");
                }
            }
        }
        return null;
    }
}

internal sealed record CodexCredentials
{
    public string? ApiKey { get; init; }
    public bool IsValid => !string.IsNullOrEmpty(ApiKey);
    public string GetAuthHeader() => $"Bearer {ApiKey}";
}