using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IIndicatorSettingsService
{
    Task<StockIndicatorSettings> GetOrCreateAsync(string symbol, CancellationToken cancellationToken = default);
    Task SaveAsync(StockIndicatorSettings settings, CancellationToken cancellationToken = default);
    Task ResetToDefaultAsync(string symbol, CancellationToken cancellationToken = default);
}
