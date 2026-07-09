using AIUsageHub.Models;
using AIUsageHub.Providers;

namespace AIUsageHub.Services;

public sealed class RefreshService : IDisposable
{
    private readonly IEnumerable<IProviderRuntime> _providers;
    private readonly CacheService _cache;
    private readonly ConfigManager _config;
    private readonly HashSet<string> _enabledProviders;
    private Timer? _timer;
    private readonly object _lock = new();

    public event Action<string, ProviderSnapshot>? ProviderUpdated;
    public event Action? RefreshComplete;

    public RefreshService(
        IEnumerable<IProviderRuntime> providers,
        CacheService cache,
        ConfigManager config)
    {
        _providers = providers;
        _cache = cache;
        _config = config;
        _enabledProviders = config.EnabledProviders;
    }

    public void Start()
    {
        var interval = TimeSpan.FromMinutes(_config.Settings.RefreshIntervalMinutes);
        _timer = new Timer(async _ =>
        {
            try { await RefreshAllAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Refresh] loop error: {ex}"); }
        }, null, TimeSpan.Zero, interval);
    }

    public void UpdateInterval(int minutes)
    {
        if (minutes <= 0) minutes = 5;
        var span = TimeSpan.FromMinutes(minutes);
        _timer?.Change(span, span);
    }

    public async Task RefreshAllAsync(bool force = false)
    {
        var tasks = _providers
            .Where(p => _enabledProviders.Contains(p.Name) && p.HasLocalCredentials())
            .Select(p => RefreshProviderAsync(p, force));

        await Task.WhenAll(tasks);
        RefreshComplete?.Invoke();
    }

    public async Task RefreshProviderAsync(IProviderRuntime provider, bool force = false)
    {
        if (force || _cache.IsStale(provider.Name))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var snapshot = await provider.RefreshAsync(cts.Token);
                _cache.Set(provider.Name, snapshot);
                ProviderUpdated?.Invoke(provider.Name, snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Refresh] {provider.Name} failed: {ex.Message}");
                var error = ProviderSnapshot.Failed(provider.Name, ex.Message);
                _cache.Set(provider.Name, error);
                ProviderUpdated?.Invoke(provider.Name, error);
            }
        }
    }

    public void Stop() => _timer?.Dispose();

    public void Dispose() => Stop();
}