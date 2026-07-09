using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIUsageHub.Models;
using AIUsageHub.Services;
using AIUsageHub.Stores;

namespace AIUsageHub.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly WidgetStore _widgetStore;
    private readonly RefreshService _refreshService;
    private readonly ConfigManager _config;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasNoProviders;
    [ObservableProperty] private bool _showWelcome;

    public ObservableCollection<ProviderCardData> Providers => _widgetStore.Providers;

    public Action<SettingsTab>? OpenSettingsRequested { get; set; }

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke(SettingsTab.General);

    [RelayCommand]
    private void OpenApiKeysSettings() => OpenSettingsRequested?.Invoke(SettingsTab.ApiKeys);

    public DashboardViewModel(
        WidgetStore widgetStore,
        RefreshService refreshService,
        ConfigManager config)
    {
        _widgetStore = widgetStore;
        _refreshService = refreshService;
        _config = config;

        _refreshService.ProviderUpdated += OnProviderUpdated;
        _refreshService.RefreshComplete += OnRefreshComplete;

        // Initial state check
        HasNoProviders = Providers.Count == 0;
        _widgetStore.Providers.CollectionChanged += (_, _) =>
        {
            HasNoProviders = Providers.Count == 0;
        };

        UpdateShowWelcome();
    }

    private void UpdateShowWelcome()
    {
        ShowWelcome = HasNoProviders && _config.ApiKeys.Count == 0;
    }

    partial void OnHasNoProvidersChanged(bool value) => UpdateShowWelcome();

    public Action? ScrollToTopRequested { get; set; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        await _refreshService.RefreshAllAsync(force: true);
        IsLoading = false;
        ScrollToTopRequested?.Invoke();
    }

    private void OnProviderUpdated(string name, ProviderSnapshot snapshot)
    {
        _widgetStore.UpdateProvider(name, snapshot);
    }

    private void OnRefreshComplete()
    {
        IsLoading = false;
    }
}
