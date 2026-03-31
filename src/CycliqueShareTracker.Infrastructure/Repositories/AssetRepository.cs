using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _dbContext;

    public AssetRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Asset> GetOrCreateAsync(string symbol, string name, string market, CancellationToken cancellationToken = default)
    {
        var asset = await _dbContext.Assets.FirstOrDefaultAsync(a => a.Symbol == symbol, cancellationToken);
        if (asset is not null)
        {
            return asset;
        }

        asset = new Asset { Symbol = symbol, Name = name, Market = market };
        _dbContext.Assets.Add(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return asset;
    }
}
