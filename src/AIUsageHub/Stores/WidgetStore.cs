using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AIUsageHub.Helpers;
using AIUsageHub.Models;
using AIUsageHub.Providers;
using AIUsageHub.Services;

namespace AIUsageHub.Stores;

public sealed class WidgetStore : INotifyPropertyChanged
{
    private readonly CacheService _cache;
    private readonly IEnumerable<IProviderRuntime> _providers;
    private readonly HashSet<string> _enabledProviders;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProviderCardData> Providers { get; } = new();

    public WidgetStore(CacheService cache, IEnumerable<IProviderRuntime> providers, ConfigManager config)
    {
        _cache = cache;
        _providers = providers;
        _enabledProviders = config.EnabledProviders;
    }

    public void UpdateFromCache()
    {
        var newCards = new List<ProviderCardData>();
        foreach (var provider in _providers)
        {
            if (!_enabledProviders.Contains(provider.Name)) continue;
            if (!provider.HasLocalCredentials()) continue;
            var snapshot = _cache.Get(provider.Name);
            if (snapshot == null) continue;

            newCards.Add(new ProviderCardData(provider, snapshot));
        }
        newCards.Sort((a, b) => string.Compare(a.ProviderName, b.ProviderName, StringComparison.Ordinal));

        Application.Current?.Dispatcher.Invoke(() =>
        {
            Providers.Clear();
            foreach (var card in newCards)
                Providers.Add(card);
        });
        OnPropertyChanged(nameof(Providers));
    }

    public void UpdateProvider(string name, ProviderSnapshot snapshot)
    {
        var existing = Providers.FirstOrDefault(p => p.ProviderName == name);
        if (existing != null)
        {
            existing.Update(snapshot);
        }
        else
        {
            var provider = _providers.FirstOrDefault(p => p.Name == name);
            if (provider != null)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var card = new ProviderCardData(provider, snapshot);
                    int index = 0;
                    while (index < Providers.Count &&
                           string.Compare(Providers[index].ProviderName, name, StringComparison.Ordinal) < 0)
                        index++;
                    Providers.Insert(index, card);
                });
            }
        }
        OnPropertyChanged(nameof(Providers));
    }

    public void RemoveStale()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            for (int i = Providers.Count - 1; i >= 0; i--)
            {
                var card = Providers[i];
                var provider = _providers.FirstOrDefault(p => p.Name == card.ProviderName);
                if (provider == null
                    || !_enabledProviders.Contains(provider.Name)
                    || !provider.HasLocalCredentials())
                {
                    Providers.RemoveAt(i);
                }
            }
        });
        OnPropertyChanged(nameof(Providers));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// View-model friendly data for a provider card in the dashboard
public sealed class ProviderCardData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private ProviderSnapshot _snapshot;

    public string ProviderName { get; }
    public string BrandColor { get; }
    public string BrandIcon { get; }
    public string? LoginUrl { get; }
    public string? Plan => _snapshot.Plan;
    public string? Error => _snapshot.Error;
    public IReadOnlyList<MetricLine> Lines => _snapshot.Lines;
    public DateTime RefreshedAt => _snapshot.RefreshedAt;

    public ProviderCardData(IProviderRuntime provider, ProviderSnapshot snapshot)
    {
        ProviderName = provider.Name;
        BrandColor = provider.BrandColor;
        BrandIcon = $"/Resources/Providers/{provider.Name.ToLowerInvariant().Replace(".", "").Replace(" ", "")}.svg";
        LoginUrl = ProviderLoginUrls.Get(provider.Name);
        _snapshot = snapshot;
    }

    public void Update(ProviderSnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(RefreshedAt));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}