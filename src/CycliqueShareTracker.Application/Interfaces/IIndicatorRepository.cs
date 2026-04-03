using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IIndicatorRepository
{
    Task UpsertIndicatorsAsync(int assetId, IReadOnlyList<DailyIndicator> indicators, CancellationToken cancellationToken = default);
    Task<DailyIndicator?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyIndicator>> GetIndicatorsAsync(int assetId, int maxRows, CancellationToken cancellationToken = default);
}
