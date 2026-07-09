using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIUsageHub.Providers;
using AIUsageHub.Services;

namespace AIUsageHub.Stores;

public sealed class ProviderEnableStore : INotifyPropertyChanged
{
    private readonly ConfigManager _config;
    private readonly IEnumerable<IProviderRuntime> _providers;
    private HashSet<string> _enabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public HashSet<string> Enabled => _enabled;

    public ProviderEnableStore(ConfigManager config, IEnumerable<IProviderRuntime> providers)
    {
        _config = config;
        _providers = providers;
        _enabled = new HashSet<string>(config.EnabledProviders);

        // Auto-detect on first run (empty enabled set)
        if (_enabled.Count == 0)
            AutoDetect();
    }

    private void AutoDetect()
    {
        foreach (var provider in _providers)
        {
            if (provider.HasLocalCredentials())
                _enabled.Add(provider.Name);
        }

        Save();
    }

    public bool IsEnabled(string providerName) => _enabled.Contains(providerName);

    public void Toggle(string providerName)
    {
        if (_enabled.Contains(providerName))
            _enabled.Remove(providerName);
        else
            _enabled.Add(providerName);

        Save();
        OnPropertyChanged(nameof(Enabled));
    }

    public void AddNewProvider(string providerName)
    {
        if (_enabled.Contains(providerName)) return;
        // Only auto-enable if the user actually has the tool
        var provider = _providers.FirstOrDefault(p => p.Name == providerName);
        if (provider != null && provider.HasLocalCredentials())
        {
            _enabled.Add(providerName);
            Save();
            OnPropertyChanged(nameof(Enabled));
        }
    }

    private void Save()
    {
        _config.EnabledProviders = new HashSet<string>(_enabled);
        _config.Save();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}