using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Infrastructure.Providers;

public interface IMarketDataSource
{
    string Name { get; }
    Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default);
}
