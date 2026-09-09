using Lunaqua.Services;

namespace Lunaqua.Tests;

/// <summary>可控的更新服务（测试用，不打网络）。</summary>
internal sealed class FakeUpdateService : IUpdateService
{
    public string CurrentVersion { get; set; } = "0.0.0";

    public bool IsInstalled { get; set; }

    public bool IsUpdatePendingRestart { get; set; }

    public AppUpdate? AvailableUpdate { get; set; }

    public AppUpdate? RollbackTarget { get; set; }

    public int CheckCount { get; private set; }

    public int DownloadCount { get; private set; }

    public int ApplyCount { get; private set; }

    public Task<AppUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        CheckCount++;
        return Task.FromResult(AvailableUpdate);
    }

    public Task DownloadAsync(AppUpdate update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        DownloadCount++;
        progress?.Report(50);
        return Task.CompletedTask;
    }

    public Task ApplyAndRestartAsync(AppUpdate update, CancellationToken cancellationToken = default)
    {
        ApplyCount++;
        return Task.CompletedTask;
    }

    public Task<AppUpdate?> FindRollbackTargetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(RollbackTarget);
}
