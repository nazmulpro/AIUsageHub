namespace AIUsageHub.Models;

public enum ThemeMode
{
    Light,
    Dark,
    System
}

public class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool LaunchAtStartup { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool ShowMinimalView { get; set; } = false;
}