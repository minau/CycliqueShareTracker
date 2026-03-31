using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IPriceRepository
{
    Task UpsertDailyPricesAsync(int assetId, IReadOnlyList<PriceBar> prices, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyPrice>> GetPricesAsync(int assetId, int maxRows, CancellationToken cancellationToken = default);
    Task<DailyPrice?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default);
}
