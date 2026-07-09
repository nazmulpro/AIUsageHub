namespace AIUsageHub.Helpers;

/// Maps provider names to the login/account page users can open from the dashboard.
public static class ProviderLoginUrls
{
    public static string? Get(string? name) => name switch
    {
        "Claude" => "https://claude.ai/",
        "Codex" => "https://platform.openai.com/account",
        "Cursor" => "https://cursor.com/dashboard",
        "Grok" => "https://console.x.ai/",
        "Devin" => "https://devin.ai/",
        "OpenRouter" => "https://openrouter.ai/keys",
        "Z.ai" => "https://z.ai/",
        _ => null
    };
}
