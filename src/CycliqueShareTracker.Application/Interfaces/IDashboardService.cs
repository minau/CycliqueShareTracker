using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(bool includeMacdInScoring = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(bool includeMacdInScoring = true, CancellationToken cancellationToken = default);
}
