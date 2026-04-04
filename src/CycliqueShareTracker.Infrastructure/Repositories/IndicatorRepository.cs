using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Repositories;

public sealed class IndicatorRepository : IIndicatorRepository
{
    private readonly AppDbContext _dbContext;

    public IndicatorRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertIndicatorsAsync(int assetId, IReadOnlyList<DailyIndicator> indicators, CancellationToken cancellationToken = default)
    {
        if (indicators.Count == 0) return;

        var minDate = indicators.Min(x => x.Date);
        var maxDate = indicators.Max(x => x.Date);

        var existing = await _dbContext.DailyIndicators
            .Where(x => x.AssetId == assetId && x.Date >= minDate && x.Date <= maxDate)
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        foreach (var item in indicators)
        {
            if (existing.TryGetValue(item.Date, out var row))
            {
                row.Sma50 = item.Sma50;
                row.Sma200 = item.Sma200;
                row.Rsi14 = item.Rsi14;
                row.Drawdown52WeeksPercent = item.Drawdown52WeeksPercent;
                row.MacdLine = item.MacdLine;
                row.MacdSignalLine = item.MacdSignalLine;
                row.MacdHistogram = item.MacdHistogram;
            }
            else
            {
                _dbContext.DailyIndicators.Add(item);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<DailyIndicator?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
    {
        return _dbContext.DailyIndicators
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyIndicator>> GetIndicatorsAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyIndicators
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }
}
