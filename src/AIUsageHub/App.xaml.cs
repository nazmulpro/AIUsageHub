using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AIUsageHub.Helpers;
using AIUsageHub.Models;
using AIUsageHub.Providers;
using AIUsageHub.Services;
using AIUsageHub.Stores;
using AIUsageHub.Views;
using AIUsageHub.ViewModels;

namespace AIUsageHub;

public partial class App : Application
{
    private ServiceProvider? _sp;
    public ServiceProvider Services => _sp!;
    private ConfigManager? _config;
    private TrayPopupWindow? _popup;
    private DashboardViewModel? _dashboardVm;
    private RefreshService? _refreshService;
    private LocalApiService? _localApi;
    private SettingsView? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, ev) =>
        {
            Log($"[DispatcherUnhandledException] {ev.Exception}");
            ev.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            Log($"[AppDomain.UnhandledException] {ev.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (s, ev) =>
        {
            Log($"[UnobservedTaskException] {ev.Exception}");
            ev.SetObserved();
        };
        Log("Startup begin");

        // Single instance check
        var existing = System.Diagnostics.Process.GetProcessesByName("AIUsageHub")
            .FirstOrDefault(p => p.Id != Environment.ProcessId);
        if (existing != null)
        {
            Current.Shutdown();
            return;
        }

        // Setup DI
        _sp = ConfigureServices();
        _config = _sp.GetRequiredService<ConfigManager>();
        ApplyCurrentTheme(_config.Settings.Theme);

        // Initialize services
        _refreshService = _sp.GetRequiredService<RefreshService>();
        _localApi = _sp.GetRequiredService<LocalApiService>();

        // Build ViewModel
        _dashboardVm = _sp.GetRequiredService<DashboardViewModel>();
        _dashboardVm.OpenSettingsRequested = OpenSettings;
        var widgetStore = _sp.GetRequiredService<WidgetStore>();

        // Load cached data immediately
        widgetStore.UpdateFromCache();

        // Start refresh loop and local API
        _refreshService.ProviderUpdated += (name, snapshot) =>
        {
            Dispatcher.Invoke(() => widgetStore.UpdateProvider(name, snapshot));
        };
        _refreshService.RefreshComplete += () =>
        {
            Dispatcher.Invoke(() => widgetStore.UpdateFromCache());
        };
        _refreshService.Start();

        try { _localApi.Start(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Local API failed to start: {ex.Message}");
        }

        // Setup popup and show it as a persistent taskbar window
        _popup = new TrayPopupWindow();
        _popup.SetViewModel(_dashboardVm);
        WindowPlacement.PositionNearTray(_popup, 410, 580);
        _popup.Show();
        Log("Startup complete");
    }

    private void OpenSettings(SettingsTab initialTab)
    {
        try
        {
            var settingsVm = _sp!.GetRequiredService<SettingsViewModel>();
            settingsVm.SelectedTab = initialTab;

            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsView(settingsVm) { Topmost = true };

                // Lower the dashboard so the Settings window is guaranteed
                // to render above it (z-order), and make it non-interactive.
                var prevTopmost = _popup?.Topmost ?? false;
                if (_popup != null)
                {
                    _popup.Topmost = false;
                    _popup.IsSettingsOpen = true;
                }

                _settingsWindow.Closed += (s, e) =>
                {
                    _settingsWindow = null;
                    if (_popup != null)
                    {
                        _popup.Topmost = prevTopmost;
                        _popup.IsSettingsOpen = false;
                    }
                };
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            Log($"OpenSettings error: {ex}");
        }
    }

    public static void ApplyCurrentTheme(Models.ThemeMode theme)
    {
        var themeDict = theme switch
        {
            Models.ThemeMode.Dark => "Resources/Styles/DarkTheme.xaml",
            Models.ThemeMode.Light => "Resources/Styles/LightTheme.xaml",
            Models.ThemeMode.System => IsSystemDarkTheme()
                ? "Resources/Styles/DarkTheme.xaml"
                : "Resources/Styles/LightTheme.xaml",
            _ => "Resources/Styles/LightTheme.xaml"
        };

        // Replace theme dictionary
        var existing = Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme") == true);
        if (existing != null)
            Current.Resources.MergedDictionaries.Remove(existing);

        Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themeDict, UriKind.Relative) });
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Config
        services.AddSingleton<ConfigManager>();

        // Http
        services.AddSingleton<HttpService>();
        services.AddHttpClient();

        // Services
        services.AddSingleton<CacheService>();
        services.AddSingleton<LocalApiService>();

        // Providers
        ProviderFactory.RegisterAll(services);

        // Stores
        services.AddSingleton<WidgetStore>();
        services.AddSingleton<LayoutStore>();
        services.AddSingleton<ProviderEnableStore>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Refresh service (last, depends on everything above)
        services.AddSingleton<RefreshService>();

        return services.BuildServiceProvider();
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            // Use Windows registry to detect dark mode
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int i) return i == 0;
            }
        }
        catch { }
        return false;
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIUsageHub");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "run.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _refreshService?.Dispose();
        _localApi?.Dispose();
        _sp?.Dispose();
        base.OnExit(e);
    }
}