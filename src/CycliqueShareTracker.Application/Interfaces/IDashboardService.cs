using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IDashboardService
{
    IReadOnlyList<TrackedAssetOptions> GetTrackedAssets();
    Task<IReadOnlyList<AssetSnapshotResult>> GetWatchlistSnapshotsAsync(AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default);
    Task<DashboardSnapshot> GetSnapshotAsync(string symbol, AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default);
    Task SaveIndicatorSettingsAsync(string symbol, IndicatorComputationSettings settings, CancellationToken cancellationToken = default);
    Task ResetIndicatorSettingsAsync(string symbol, CancellationToken cancellationToken = default);
}
