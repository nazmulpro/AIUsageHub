using System.Text.Json;

namespace AIUsageHub.Providers.Cursor;

internal static class CursorAuth
{
    private static readonly string CursorAppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cursor");

    private static readonly string UserDir = Path.Combine(CursorAppData, "User");
    private static readonly string GlobalStorageDir = Path.Combine(CursorAppData, "globalStorage", "storage.json");

    public static bool CredentialsExist()
    {
        if (File.Exists(GlobalStorageDir)) return true;
        if (Directory.Exists(UserDir))
            return Directory.EnumerateFiles(UserDir, "*.json").Any();
        return false;
    }

    public static CursorCredentials? LoadCredentials()
    {
        // Try globalStorage first
        if (File.Exists(GlobalStorageDir))
        {
            try
            {
                var json = File.ReadAllText(GlobalStorageDir);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("authToken", out var token))
                    return new CursorCredentials { AuthToken = token.GetString() };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CursorAuth] Failed globalStorage: {ex.Message}");
            }
        }

        // Try user settings
        if (Directory.Exists(UserDir))
        {
            foreach (var file in Directory.GetFiles(UserDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("cursorAuth", out var auth) ||
                        doc.RootElement.TryGetProperty("authToken", out auth))
                        return new CursorCredentials { AuthToken = auth.GetString() };
                }
                catch { }
            }
        }
        return null;
    }
}

internal sealed record CursorCredentials
{
    public string? AuthToken { get; init; }
    public bool IsValid => !string.IsNullOrEmpty(AuthToken);
    public string GetAuthHeader() => $"Bearer {AuthToken}";
}