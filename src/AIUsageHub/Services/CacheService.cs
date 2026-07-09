using AIUsageHub.Models;

namespace AIUsageHub.Services;

public sealed class CacheService
{
    private readonly Dictionary<string, CachedSnapshot> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(4); // Refresh every 5 min, serve stale for 4

    public ProviderSnapshot? Get(string providerName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(providerName, out var cached))
                return cached.Snapshot;
            return null;
        }
    }

    public void Set(string providerName, ProviderSnapshot snapshot)
    {
        lock (_cache)
        {
            _cache[providerName] = new CachedSnapshot(snapshot, DateTime.UtcNow);
        }
    }

    public bool IsStale(string providerName)
    {
        lock (_cache)
        {
            if (!_cache.TryGetValue(providerName, out var cached)) return true;
            return DateTime.UtcNow - cached.CachedAt > _ttl;
        }
    }

    public void Clear(string? providerName = null)
    {
        lock (_cache)
        {
            if (providerName != null)
                _cache.Remove(providerName);
            else
                _cache.Clear();
        }
    }

    public string[] GetCachedProviderNames()
    {
        lock (_cache)
            return _cache.Keys.ToArray();
    }

    private record CachedSnapshot(ProviderSnapshot Snapshot, DateTime CachedAt);
}