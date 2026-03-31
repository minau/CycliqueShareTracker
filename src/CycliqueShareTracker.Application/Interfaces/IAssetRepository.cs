using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IAssetRepository
{
    Task<Asset> GetOrCreateAsync(string symbol, string name, string market, CancellationToken cancellationToken = default);
}
