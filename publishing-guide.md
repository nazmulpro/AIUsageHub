# Publishing AIUsageHub with Velopack + GitHub Releases

## Complete Guide for .NET WPF Windows Desktop Apps

---

## Table of Contents

1. [Understanding the Toolchain](#1-understanding-the-toolchain)
2. [Prerequisites](#2-prerequisites)
3. [Project Configuration Changes](#3-project-configuration-changes)
4. [Integrating Velopack into the App](#4-integrating-velopack-into-the-app)
5. [Creating the UpdateService](#5-creating-the-updateservice)
6. [Adding Update UI to Settings](#6-adding-update-ui-to-settings)
7. [GitHub Actions CI/CD Pipeline](#7-github-actions-cicd-pipeline)
8. [Versioning Strategy](#8-versioning-strategy)
9. [Release Workflow](#9-release-workflow)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Understanding the Toolchain

### What is Velopack?

[Velopack](https://velopack.io/) is a modern, cross-platform update and installation framework for .NET applications. It's the successor to Squirrel.Windows and offers:

- **Installer generation** — Creates a Setup.exe with all dependencies bundled
- **Delta updates** — Only downloads the changed bytes between versions
- **Auto-update** — Built-in update checking and applying
- **GitHub Releases integration** — Works natively with GitHub
- **Lifecycle hooks** — Runs code on install, update, uninstall, and first-run

### How It Works

```
Developer pushes tag v1.1.0
        │
        ▼
GitHub Actions runs
        │
        ├── dotnet publish → produces AIUsageHub.exe + DLLs
        │
        ├── vpk pack → produces:
        │   ├── Setup.exe        (installer)
        │   ├── RELEASES         (update metadata)
        │   ├── AIUsageHub-1.1.0-full.nupkg   (full package)
        │   └── AIUsageHub-1.1.0-delta.nupkg  (delta from last version)
        │
        └── GitHub Release created with all files attached

User's app checks GitHub Releases
        │
        ▼
New version found? Download delta/full nupkg
        │
        ▼
Apply update → app restarts on new version
```

---

## 2. Prerequisites

### Tools to Install (on your dev machine)

```powershell
# .NET 10 SDK
# Download from: https://dotnet.microsoft.com/download/dotnet/10.0

# Velopack CLI tool (vpk)
dotnet tool install -g vpk

# Verify
vpk --version
dotnet --version
```

### GitHub Repository Setup

1. Repo: `https://github.com/nazmulpro/AIUsageHub`
2. No special tokens needed for **public** repositories
3. For **private** repos, create a GitHub Personal Access Token with `repo` scope

---

## 3. Project Configuration Changes

### 3.1 Update AIUsageHub.csproj

Final state of `src/AIUsageHub/AIUsageHub.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Resources\Icons\app.ico</ApplicationIcon>
    <AssemblyName>AIUsageHub</AssemblyName>
    <RootNamespace>AIUsageHub</RootNamespace>
    <Version>1.0.0</Version>
    <Authors>Nazmul Hossain</Authors>
    <Description>Track your AI coding subscriptions from a lightweight desktop dashboard</Description>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <PackageIcon>Resources\Icons\app.ico</PackageIcon>
    <PackageDescription>AIUsageHub - Monitor your AI coding service usage</PackageDescription>
    <PackageTags>ai;usage;monitor;wpf</PackageTags>
    <Company>AIUsageHub</Company>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Net.Http" />
    <Using Include="System.IO" />
    <Using Include="System.Text.Json" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="CredentialManagement" Version="1.0.2" />
    <PackageReference Include="SharpVectors.Wpf" Version="1.8.5" />
    <PackageReference Include="Velopack" Version="1.2.0" />
  </ItemGroup>

  <ItemGroup>
    <Resource Include="Resources\Icons\app.ico" />
    <Resource Include="Resources\Providers\*.svg" />
    <Resource Include="Resources\Styles\DarkTheme.xaml" />
    <Resource Include="Resources\Styles\LightTheme.xaml" />
  </ItemGroup>

</Project>
```

### 3.2 Key Changes Explained

| Change | Why |
|---|---|
| `SelfContained=false` → `true` | Bundles .NET runtime so users don't need to install it separately |
| Added `PackageIcon`, `PackageDescription`, `PackageTags`, `Authors`, `Company` | Required by Velopack for NuGet metadata |
| Added `Velopack` NuGet package | Provides the update/install runtime library |

---

## 4. Integrating Velopack into the App

### 4.1 Modify App.xaml.cs

Velopack must be initialized before any WPF UI code runs. Add this constructor to `src/AIUsageHub/App.xaml.cs`:

```csharp
using Velopack;

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

    public App()
    {
        VelopackApp.Build()
            .OnFirstRun(v => Dispatcher.Invoke(() => MessageBox.Show(
                "Welcome to AIUsageHub!", "Thank you for installing",
                MessageBoxButton.OK, MessageBoxImage.Information)))
            .Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // ... existing startup code ...
    }
}
```

The constructor runs before `OnStartup`, so `VelopackApp.Build().Run()` executes before any WPF window creation. During install/uninstall, `Run()` may call `Exit()` after running callbacks, which prevents the app from loading unnecessarily.

### 4.2 Register UpdateService in DI

In `App.xaml.cs` `ConfigureServices()`:

```csharp
private static ServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();
    // ... existing registrations ...
    services.AddSingleton<CacheService>();
    services.AddSingleton<LocalApiService>();
    services.AddSingleton<UpdateService>();
    // ... remaining registrations ...
    return services.BuildServiceProvider();
}
```

### 4.3 Background Update Check at Startup

After DI setup in `OnStartup`:

```csharp
var updateSvc = _sp.GetRequiredService<UpdateService>();
_ = CheckForUpdatesAtStartupAsync(updateSvc);

// ...

private static async Task CheckForUpdatesAtStartupAsync(UpdateService updateService)
{
    try
    {
        await Task.Delay(5000);
        await updateService.CheckForUpdatesAsync();
    }
    catch
    {
        // Silently fail — user can manual-check in Settings
    }
}
```

This runs a silent check 5 seconds after launch. The result is cached in `UpdateService` and displayed when the user opens the About tab.

---

## 5. Creating the UpdateService

### 5.1 File: src/AIUsageHub/Services/UpdateService.cs

```csharp
using Velopack;
using Velopack.Sources;

namespace AIUsageHub.Services;

public class UpdateService
{
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _cachedUpdateInfo;

    public UpdateService()
    {
        var source = new GithubSource(
            "https://github.com/nazmulpro/AIUsageHub",
            null,    // accessToken — null for public repos
            false);  // prereleases — false to only include stable releases
        _updateManager = new UpdateManager(source);
    }

    public string CurrentVersion =>
        _updateManager.CurrentVersion?.ToString() ?? "1.0.0";

    public bool IsUpdatePending =>
        _updateManager.UpdatePendingRestart != null;

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        _cachedUpdateInfo = await _updateManager.CheckForUpdatesAsync();
        return _cachedUpdateInfo;
    }

    public async Task DownloadUpdateAsync(Action<int>? progress = null)
    {
        if (_cachedUpdateInfo == null)
            throw new InvalidOperationException("Call CheckForUpdatesAsync first.");
        await _updateManager.DownloadUpdatesAsync(_cachedUpdateInfo, progress);
    }

    public void ApplyUpdateAndRestart()
    {
        if (_cachedUpdateInfo?.TargetFullRelease == null)
            throw new InvalidOperationException("Call CheckForUpdatesAsync first.");
        _updateManager.ApplyUpdatesAndRestart(_cachedUpdateInfo.TargetFullRelease);
    }
}
```

### 5.2 Key Notes for Velopack 1.2.0

| API | Signature |
|---|---|
| `CheckForUpdatesAsync()` | Returns `Task<UpdateInfo?>` — null if up-to-date |
| `DownloadUpdatesAsync(info, progress)` | `progress` is `Action<int>?` (0–100), not `IProgress<double>` |
| `ApplyUpdatesAndRestart(asset)` | Takes `VelopackAsset` (from `UpdateInfo.TargetFullRelease`) |
| `UpdatePendingRestart` | Property returning `VelopackAsset?` — replaces `IsPendingUpdate` |

---

## 6. Adding Update UI to Settings

### 6.1 Add "About" to SettingsTab Enum

`src/AIUsageHub/Models/SettingsTab.cs`:

```csharp
namespace AIUsageHub.Models;

public enum SettingsTab
{
    General,
    ApiKeys,
    About,
}
```

### 6.2 Extend SettingsViewModel

Add `UpdateService` as a constructor dependency and add update-related properties/commands to `src/AIUsageHub/ViewModels/SettingsViewModel.cs`:

```csharp
private readonly UpdateService _updateService;

// Add to existing constructor parameters:
public SettingsViewModel(
    ConfigManager config,
    ProviderEnableStore enableStore,
    RefreshService refreshService,
    WidgetStore widgetStore,
    CacheService cacheService,
    UpdateService updateService)
{
    // ... existing init ...
    _updateService = updateService;
    _currentVersion = $"v{_updateService.CurrentVersion}";
}

// Observable properties to add:
[ObservableProperty] private string _currentVersion = "v1.0.0";
[ObservableProperty] private string _updateStatus = "";
[ObservableProperty] private bool _isCheckingForUpdates;
[ObservableProperty] private bool _isDownloading;
[ObservableProperty] private double _downloadProgress;
[ObservableProperty] private bool _updateAvailable;
[ObservableProperty] private string _newVersion = "";

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
            UpdateStatus = "You're up to date!";
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
```

### 6.3 About Tab in SettingsView.xaml

Add a third tab button in the segmented bar:

```xml
<StackPanel Orientation="Horizontal">
    <Button Style="{StaticResource SettingsTabGeneral}" .../>
    <Button Style="{StaticResource SettingsTabApiKeys}" .../>
    <Button Style="{StaticResource SettingsTabAbout}"
            Command="{Binding SetTabCommand}"
            CommandParameter="{x:Static models:SettingsTab.About}">
        <TextBlock Text="About" FontSize="{DynamicResource FontSizeSmall}"
                   FontFamily="{DynamicResource FontFamily}"/>
    </Button>
</StackPanel>
```

Add the tab style in Window.Resources:

```xml
<Style x:Key="SettingsTabAbout" TargetType="Button" BasedOn="{StaticResource SettingsTab}">
    <Style.Triggers>
        <DataTrigger Binding="{Binding SelectedTab}" Value="{x:Static models:SettingsTab.About}">
            <Setter Property="Background" Value="{DynamicResource BgTabSelected}"/>
            <Setter Property="Foreground" Value="{DynamicResource TextPrimary}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource BorderTabSelected}"/>
            <Setter Property="BorderThickness" Value="1"/>
        </DataTrigger>
    </Style.Triggers>
</Style>
```

Add the About tab content (using existing DynamicResource theme tokens):

```xml
<!-- ================= About tab ================= -->
<StackPanel>
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding SelectedTab}"
                             Value="{x:Static models:SettingsTab.About}">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>

    <TextBlock Text="Software Updates" Style="{DynamicResource SettingLabelStyle}"
               FontSize="16" FontWeight="SemiBold" Margin="0,10,0,5"/>

    <Border BorderBrush="{DynamicResource BorderLight}"
            BorderThickness="1" CornerRadius="8" Padding="15"
            Background="{DynamicResource BgBarTrack}">
        <StackPanel>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="Current Version:" VerticalAlignment="Center"
                           FontFamily="{DynamicResource FontFamily}"/>
                <TextBlock Grid.Column="1" Text="{Binding CurrentVersion}"
                           FontWeight="Bold" Margin="10,0,0,0"
                           VerticalAlignment="Center"
                           FontFamily="{DynamicResource FontFamily}"/>
            </Grid>
            <Separator Margin="0,10"/>
            <TextBlock Text="{Binding UpdateStatus}" Margin="0,5"
                       Foreground="{DynamicResource TextTertiary}"
                       FontFamily="{DynamicResource FontFamily}"/>
            <ProgressBar Value="{Binding DownloadProgress}" Maximum="100"
                         Height="4" Margin="0,5"
                         Visibility="{Binding IsDownloading,
                             Converter={StaticResource BoolToVis}}"/>
            <Button Command="{Binding CheckForUpdatesCommand}"
                    Content="Check for Updates" Margin="0,5" Padding="10,5"
                    Cursor="Hand" FontFamily="{DynamicResource FontFamily}"
                    FontSize="{DynamicResource FontSizeBody}"/>
            <Button Command="{Binding InstallUpdateCommand}"
                    Content="Download &amp; Install"
                    Visibility="{Binding UpdateAvailable,
                        Converter={StaticResource BoolToVis}}"
                    Margin="0,5" Padding="10,5" Cursor="Hand"
                    Background="{DynamicResource BgSaveButton}"
                    FontFamily="{DynamicResource FontFamily}"
                    FontSize="{DynamicResource FontSizeBody}"/>
        </StackPanel>
    </Border>

    <TextBlock Text="AIUsageHub" Style="{DynamicResource SettingLabelStyle}"
               FontSize="16" FontWeight="SemiBold" Margin="0,16,0,5"/>
    <TextBlock Text="Monitor your AI coding service usage..."
               Style="{DynamicResource SettingDescStyle}"/>
    <TextBlock Margin="0,8,0,0" FontFamily="{DynamicResource FontFamily}"
               FontSize="{DynamicResource FontSizeSmall}"
               Foreground="{DynamicResource TextTertiary}">
        <Hyperlink NavigateUri="https://github.com/nazmulpro/AIUsageHub"
                   RequestNavigate="OnHyperlinkClick">
            View on GitHub
        </Hyperlink>
    </TextBlock>
</StackPanel>
```

Key differences from a generic XAML approach:
- Uses `{DynamicResource ...}` theme tokens from the app's dark/light theme system
- Uses `{StaticResource BoolToVis}` (registered globally in `App.xaml`)
- `{DynamicResource BgSaveButton}` matches the existing "Save" button style
- The `InverseBoolConverter` is registered globally in `App.xaml` alongside `BoolToVis`

---

## 7. GitHub Actions CI/CD Pipeline

### 7.1 Workflow File

`.github/workflows/release.yml`:

```yaml
name: Build and Publish Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Velopack
        run: dotnet tool install -g vpk

      - name: Publish Application
        run: |
          dotnet publish src/AIUsageHub/AIUsageHub.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            --output publish

      - name: Create Velopack Release
        shell: pwsh
        run: |
          $VERSION = "${{ github.ref_name }}" -replace '^v', ''
          $REPO_URL = "${{ github.server_url }}/${{ github.repository }}"
          vpk pack `
            --packId "AIUsageHub" `
            --packTitle "AI Usage Hub" `
            --packAuthors "Nazmul Hossain" `
            --packVersion $VERSION `
            --outputDir "packaged" `
            --packDir "publish" `
            --mainExe "AIUsageHub.exe" `
            --releaseNotes "Full changelog: $REPO_URL/releases/tag/${{ github.ref_name }}"

      - name: Create Release
        uses: softprops/action-gh-release@v2
        with:
          files: packaged/*
          generate_release_notes: true
          fail_on_unmatched_files: true
```

### 7.2 Generated Files

After `vpk pack`:

| File | Size | Purpose |
|---|---|---|
| `Setup.exe` | ~80 MB | Windows installer |
| `RELEASES` | ~1 KB | Update metadata |
| `AIUsageHub-1.0.0-full.nupkg` | ~80 MB | Full package |
| `AIUsageHub-1.0.0-delta.nupkg` | ~1 KB | Delta (first release has no delta) |

---

## 8. Versioning Strategy

### 8.1 Semantic Versioning

| Example | When to Bump |
|---|---|
| `1.0.0` | Initial release |
| `1.0.1` | Bug fix |
| `1.1.0` | New feature |
| `2.0.0` | Breaking change |

### 8.2 Git Tag Convention

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The `v` prefix distinguishes version tags from other tags. The CI workflow matches `v*`.

---

## 9. Release Workflow

### 9.1 Making a Release

```powershell
git checkout main
git pull origin main
# Optional: bump <Version> in .csproj
git add src/AIUsageHub/AIUsageHub.csproj
git commit -m "Bump version to 1.1.0"
git tag v1.1.0
git push origin v1.1.0
git push origin main
```

### 9.2 Testing Locally

```powershell
# Install vpk
dotnet tool install -g vpk

# Publish
dotnet publish src/AIUsageHub/AIUsageHub.csproj -c Release -r win-x64 --self-contained true -o publish

# Package
vpk pack --packId AIUsageHub --packVersion 1.0.0 --packDir publish --mainExe AIUsageHub.exe -o packaged

# Install
.\packaged\Setup.exe

# For local update testing, use SimpleSource with LocalFile:
# var source = new SimpleSource(new LocalFile("C:\\updates\\"));
```

---

## 10. Troubleshooting

### Setup.exe flagged by antivirus
Self-contained .NET apps are sometimes falsely flagged. Sign with a code signing certificate:
```
vpk sign --signParams "/fd SHA256 /a /n YourName" --packDir publish
```

### Auto-update doesn't find new versions
- Tag not pushed yet
- GitHub Release not fully processed (wait a few minutes)
- Check `prerelease` parameter if using pre-release tags

### "Access to the path is denied" during install
Run the installer as administrator, or install to a non-protected path.

---

## Appendix A: Files Changed

```
AIUsageHub/
├── .github/
│   └── workflows/
│       └── release.yml              # NEW: CI/CD pipeline
├── .gitignore                       # MODIFIED: added publish/ packaged/
├── publishing-guide.md              # MODIFIED: this file
└── src/
    └── AIUsageHub/
        ├── AIUsageHub.csproj        # MODIFIED: SelfContained, metadata, Velopack
        ├── App.xaml                 # MODIFIED: InverseBoolConverter global
        ├── App.xaml.cs              # MODIFIED: Velopack lifecycle + DI + startup check
        ├── Models/
        │   └── SettingsTab.cs       # MODIFIED: added About
        ├── Services/
        │   └── UpdateService.cs     # NEW: auto-update logic
        ├── ViewModels/
        │   └── SettingsViewModel.cs # MODIFIED: update commands + properties
        └── Views/
            └── SettingsView.xaml    # MODIFIED: About tab with update UI
            └── SettingsView.xaml.cs # MODIFIED: hyperlink handler
```

## Appendix B: Useful Commands

```powershell
# Velopack CLI
vpk pack --help
vpk pack -u AIUsageHub -v 1.0.0 -o output -p publish -e AIUsageHub.exe

# .NET CLI
dotnet publish -c Release -r win-x64 --self-contained true -o publish
dotnet build -c Release

# Git
git tag v1.0.0
git push origin v1.0.0
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```
