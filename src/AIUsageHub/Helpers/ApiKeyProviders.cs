namespace AIUsageHub.Helpers;

public sealed record ApiKeyProviderInfo(string Name, string Label, string GetUrl)
{
    public string BrandIcon => $"/Resources/Providers/{Name.ToLowerInvariant().Replace(".", "").Replace(" ", "")}.svg";
}

public static class ApiKeyProviders
{
    public static readonly ApiKeyProviderInfo[] All =
    {
        new("Claude", "Anthropic key", "https://console.anthropic.com/settings/keys"),
        new("Codex", "OpenAI key", "https://platform.openai.com/api-keys"),
        new("Cursor", "Cursor key", "https://www.cursor.com/settings/api-keys"),
        new("Grok", "xAI key", "https://console.x.ai"),
        new("OpenRouter", "OpenRouter key", "https://openrouter.ai/keys"),
        new("Z.ai", "Z.ai key", "https://z.ai/manage-apikey/apikey-list"),
    };
}
