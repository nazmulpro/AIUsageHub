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
            null,
            false);

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
