using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalRepository
{
    Task UpsertSignalsAsync(int assetId, IReadOnlyList<DailySignal> signals, CancellationToken cancellationToken = default);
    Task<DailySignal?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default);
}
