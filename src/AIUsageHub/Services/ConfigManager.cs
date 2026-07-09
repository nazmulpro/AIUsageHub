using AIUsageHub.Models;

namespace AIUsageHub.Services;

/// <summary>
/// Manages persistent configuration at %APPDATA%/AIUsageHub/config.json.
/// Handles settings, provider enablement, layout, and API keys.
/// </summary>
public sealed class ConfigManager
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIUsageHub");

    private static readonly string ConfigPath = Path.Combine(AppDataDir, "config.json");
    private static readonly string ProvidersPath = Path.Combine(AppDataDir, "providers.json");

    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();
    public HashSet<string> EnabledProviders { get; internal set; } = new();
    public Dictionary<string, string> ApiKeys { get; private set; } = new(); // provider name → key

    public event Action? SettingsChanged;

    public ConfigManager()
    {
        EnsureDirectory();
        Load();
    }

    private void EnsureDirectory()
    {
        if (!Directory.Exists(AppDataDir))
            Directory.CreateDirectory(AppDataDir);
    }

    public void Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOpts);
                if (settings != null) Settings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] Failed to load settings: {ex.Message}");
            }
        }

        if (File.Exists(ProvidersPath))
        {
            try
            {
                var json = File.ReadAllText(ProvidersPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("enabled", out var enabled))
                {
                    EnabledProviders = enabled.EnumerateArray()
                        .Select(e => e.GetString() ?? "").ToHashSet();
                }
                if (doc.RootElement.TryGetProperty("apiKeys", out var keys))
                {
                    ApiKeys = new Dictionary<string, string>();
                    foreach (var prop in keys.EnumerateObject())
                        ApiKeys[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] Failed to load providers: {ex.Message}");
            }
        }
    }

    public void Save()
    {
        try
        {
            var settingsJson = JsonSerializer.Serialize(Settings, _jsonOpts);
            File.WriteAllText(ConfigPath, settingsJson);

            var providersObj = new
            {
                enabled = EnabledProviders.ToArray(),
                apiKeys = ApiKeys
            };
            var providersJson = JsonSerializer.Serialize(providersObj, _jsonOpts);
            File.WriteAllText(ProvidersPath, providersJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] Failed to save: {ex.Message}");
        }
        SettingsChanged?.Invoke();
    }

    public void SaveSettings() => Save();
}