using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(CancellationToken cancellationToken = default);
}
