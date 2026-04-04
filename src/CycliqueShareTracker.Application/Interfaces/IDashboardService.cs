using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IDashboardService
{
    IReadOnlyList<TrackedAssetOptions> GetTrackedAssets();
    Task<IReadOnlyList<AssetSnapshotResult>> GetWatchlistSnapshotsAsync(bool includeMacdInScoring = true, CancellationToken cancellationToken = default);
    Task<DashboardSnapshot> GetSnapshotAsync(string symbol, bool includeMacdInScoring = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(string symbol, bool includeMacdInScoring = true, CancellationToken cancellationToken = default);
}
