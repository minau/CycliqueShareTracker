using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IDataProvider
{
    Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default);
}
