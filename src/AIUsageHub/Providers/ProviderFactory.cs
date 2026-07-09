using Microsoft.Extensions.DependencyInjection;
using AIUsageHub.Providers.Claude;
using AIUsageHub.Providers.Codex;
using AIUsageHub.Providers.Cursor;
using AIUsageHub.Providers.Devin;
using AIUsageHub.Providers.Grok;
using AIUsageHub.Providers.OpenRouter;
using AIUsageHub.Providers.Zai;
using AIUsageHub.Services;

namespace AIUsageHub.Providers;

public static class ProviderFactory
{
    public static void RegisterAll(IServiceCollection services)
    {
        // Providers that only need HttpClient (DI resolves it)
        services.AddSingleton<IProviderRuntime, ClaudeProvider>();
        services.AddSingleton<IProviderRuntime, CodexProvider>();
        services.AddSingleton<IProviderRuntime, CursorProvider>();
        services.AddSingleton<IProviderRuntime, DevinProvider>();
        services.AddSingleton<IProviderRuntime, GrokProvider>();

        // Providers that need ConfigManager for API keys
        services.AddSingleton<IProviderRuntime>(sp =>
            new OpenRouterProvider(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<ConfigManager>()));
        services.AddSingleton<IProviderRuntime>(sp =>
            new ZaiProvider(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<ConfigManager>()));
    }

    public static IEnumerable<IProviderRuntime> GetAll(IServiceProvider sp)
        => sp.GetServices<IProviderRuntime>();
}