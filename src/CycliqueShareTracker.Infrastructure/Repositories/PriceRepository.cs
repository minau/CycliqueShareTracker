using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Repositories;

public sealed class PriceRepository : IPriceRepository
{
    private readonly AppDbContext _dbContext;

    public PriceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertDailyPricesAsync(int assetId, IReadOnlyList<PriceBar> prices, CancellationToken cancellationToken = default)
    {
        var minDate = prices.Min(x => x.Date);
        var maxDate = prices.Max(x => x.Date);

        var existing = await _dbContext.DailyPrices
            .Where(x => x.AssetId == assetId && x.Date >= minDate && x.Date <= maxDate)
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        foreach (var p in prices)
        {
            if (existing.TryGetValue(p.Date, out var row))
            {
                row.Open = p.Open;
                row.High = p.High;
                row.Low = p.Low;
                row.Close = p.Close;
                row.Volume = p.Volume;
            }
            else
            {
                _dbContext.DailyPrices.Add(new DailyPrice
                {
                    AssetId = assetId,
                    Date = p.Date,
                    Open = p.Open,
                    High = p.High,
                    Low = p.Low,
                    Close = p.Close,
                    Volume = p.Volume
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyPrice>> GetPricesAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyPrices
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<DailyPrice>> GetPricesInRangeAsync(int assetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyPrices
            .Where(x => x.AssetId == assetId && x.Date >= startDate && x.Date <= endDate)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }

    public Task<DailyPrice?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
    {
        return _dbContext.DailyPrices
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
