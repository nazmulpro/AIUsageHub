using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIUsageHub;
using AIUsageHub.Helpers;
using AIUsageHub.Models;
using AIUsageHub.Services;
using AIUsageHub.Stores;

namespace AIUsageHub.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigManager _config;
    private readonly ProviderEnableStore _enableStore;
    private readonly RefreshService _refreshService;
    private readonly WidgetStore _widgetStore;
    private readonly CacheService _cacheService;
    private readonly UpdateService _updateService;
    private readonly DispatcherTimer _statusTimer;

    public ObservableCollection<ApiKeyRow> ApiKeys { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty] private SettingsTab _selectedTab = SettingsTab.General;
    [ObservableProperty] private ThemeMode _theme;
    [ObservableProperty] private int _refreshInterval;
    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _showMinimalView;

    [ObservableProperty]
    private string _currentVersion = "v1.0.0";

    [ObservableProperty]
    private string _updateStatus = "";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string _newVersion = "";

    public bool HasStatus => _statusMessage is not null;

    public SettingsViewModel(
        ConfigManager config,
        ProviderEnableStore enableStore,
        RefreshService refreshService,
        WidgetStore widgetStore,
        CacheService cacheService,
        UpdateService updateService)
    {
        _config = config;
        _enableStore = enableStore;
        _refreshService = refreshService;
        _widgetStore = widgetStore;
        _cacheService = cacheService;
        _updateService = updateService;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) =>
        {
            StatusMessage = null;
            _statusTimer.Stop();
        };

        foreach (var info in ApiKeyProviders.All)
        {
            _config.ApiKeys.TryGetValue(info.Name, out var existing);
            var icon = GetIconPath(info.Name, _config.Settings.Theme);
            ApiKeys.Add(new ApiKeyRow(info.Name, info.Label, info.GetUrl, icon, existing ?? ""));
        }
        _theme = config.Settings.Theme;
        _refreshInterval = config.Settings.RefreshIntervalMinutes;
        _launchAtStartup = config.Settings.LaunchAtStartup;
        _showNotifications = config.Settings.ShowNotifications;
        _showMinimalView = config.Settings.ShowMinimalView;
        _currentVersion = $"v{_updateService.CurrentVersion}";
    }

    partial void OnThemeChanged(ThemeMode value)
    {
        _config.Settings.Theme = value;
        _config.Save();
        App.ApplyCurrentTheme(value);
        RebuildApiKeys();
    }

    partial void OnRefreshIntervalChanged(int value)
    {
        _config.Settings.RefreshIntervalMinutes = value;
        _config.Save();
        _refreshService.UpdateInterval(value);
        _ = _refreshService.RefreshAllAsync(force: true);
    }

    partial void OnLaunchAtStartupChanged(bool value)
    {
        _config.Settings.LaunchAtStartup = value;
        _config.Save();
        AutoStartManager.Set(value);
    }

    partial void OnShowNotificationsChanged(bool value)
    {
        _config.Settings.ShowNotifications = value;
        _config.Save();
    }

    partial void OnShowMinimalViewChanged(bool value)
    {
        _config.Settings.ShowMinimalView = value;
        _config.Save();
    }

    private void RebuildApiKeys()
    {
        ApiKeys.Clear();
        foreach (var info in ApiKeyProviders.All)
        {
            _config.ApiKeys.TryGetValue(info.Name, out var existing);
            var icon = GetIconPath(info.Name, _config.Settings.Theme);
            ApiKeys.Add(new ApiKeyRow(info.Name, info.Label, info.GetUrl, icon, existing ?? ""));
        }
    }

    private static string GetIconPath(string name, ThemeMode theme)
    {
        var safe = name.ToLowerInvariant().Replace(".", "").Replace(" ", "");
        if (theme == ThemeMode.Light && HasLightVariant(safe))
            return $"/Resources/Providers/{safe}-light.svg";
        return $"/Resources/Providers/{safe}.svg";
    }

    private static bool HasLightVariant(string safe) => safe switch
    {
        "cursor" or "grok" or "openrouter" or "codex" => true,
        _ => false
    };

    [RelayCommand]
    private void SetTab(SettingsTab tab) => SelectedTab = tab;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        UpdateStatus = "Checking for updates...";
        UpdateAvailable = false;

        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                NewVersion = updateInfo.TargetFullRelease.Version.ToString();
                UpdateStatus = $"Version {NewVersion} is available!";
                UpdateAvailable = true;
            }
            else
            {
                UpdateStatus = "You're up to date!";
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsDownloading) return;

        IsDownloading = true;
        UpdateStatus = "Downloading update...";

        try
        {
            var progress = new Action<int>(p =>
            {
                DownloadProgress = p;
                UpdateStatus = $"Downloading... {p}%";
            });

            await _updateService.DownloadUpdateAsync(progress);

            UpdateStatus = "Restarting to apply update...";
            await Task.Delay(1000);

            _updateService.ApplyUpdateAndRestart();
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _config.Save();
    }

    [RelayCommand]
    private async Task SaveApiKeys()
    {
        var removed = new List<string>();
        foreach (var row in ApiKeys)
        {
            var key = (row.Key ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                if (_config.ApiKeys.Remove(row.Name))
                    removed.Add(row.Name);
            }
            else
            {
                _config.ApiKeys[row.Name] = key;
                _config.EnabledProviders.Add(row.Name);
            }
        }
        _config.Save();

        foreach (var name in removed)
            _cacheService.Clear(name);

        _widgetStore.RemoveStale();

        StatusMessage = "\u2713 Saved";
        _statusTimer.Start();
        await _refreshService.RefreshAllAsync(force: true);
    }
}

public sealed class ApiKeyRow
{
    public string Name { get; }
    public string Label { get; }
    public string GetUrl { get; }
    public string BrandIcon { get; }
    public string Key { get; set; }
    public ApiKeyRow(string name, string label, string getUrl, string brandIcon, string key)
        => (Name, Label, GetUrl, BrandIcon, Key) = (name, label, getUrl, brandIcon, key);
}