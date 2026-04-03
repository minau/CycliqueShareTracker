using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Repositories;

public sealed class SignalRepository : ISignalRepository
{
    private readonly AppDbContext _dbContext;

    public SignalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertSignalsAsync(int assetId, IReadOnlyList<DailySignal> signals, CancellationToken cancellationToken = default)
    {
        if (signals.Count == 0) return;

        var minDate = signals.Min(x => x.Date);
        var maxDate = signals.Max(x => x.Date);

        var existing = await _dbContext.DailySignals
            .Where(x => x.AssetId == assetId && x.Date >= minDate && x.Date <= maxDate)
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        foreach (var item in signals)
        {
            if (existing.TryGetValue(item.Date, out var row))
            {
                row.Score = item.Score;
                row.SignalLabel = item.SignalLabel;
                row.Explanation = item.Explanation;
                row.ExitScore = item.ExitScore;
                row.ExitSignalLabel = item.ExitSignalLabel;
                row.ExitPrimaryReason = item.ExitPrimaryReason;
            }
            else
            {
                _dbContext.DailySignals.Add(item);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<DailySignal?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
    {
        return _dbContext.DailySignals
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailySignal>> GetSignalsAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailySignals
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Date)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }
}
