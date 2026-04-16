using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CycliqueShareTracker.Infrastructure.Services;

public sealed class IndicatorSettingsService : IIndicatorSettingsService
{
    private readonly AppDbContext _dbContext;

    public IndicatorSettingsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StockIndicatorSettings> GetOrCreateAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var existing = await _dbContext.StockIndicatorSettings
            .SingleOrDefaultAsync(x => x.Symbol == normalizedSymbol, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var settings = StockIndicatorSettings.CreateDefault(normalizedSymbol);
        _dbContext.StockIndicatorSettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task SaveAsync(StockIndicatorSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Symbol = NormalizeSymbol(settings.Symbol);
        settings.UpdatedAtUtc = DateTime.UtcNow;

        if (settings.Id == 0)
        {
            _dbContext.StockIndicatorSettings.Add(settings);
        }
        else
        {
            _dbContext.StockIndicatorSettings.Update(settings);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetToDefaultAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreateAsync(symbol, cancellationToken);
        existing.ParabolicSarStep = StockIndicatorSettings.DefaultParabolicSarStep;
        existing.ParabolicSarMax = StockIndicatorSettings.DefaultParabolicSarMax;
        existing.BollingerPeriod = StockIndicatorSettings.DefaultBollingerPeriod;
        existing.BollingerStdDev = StockIndicatorSettings.DefaultBollingerStdDev;
        existing.MacdFastPeriod = StockIndicatorSettings.DefaultMacdFastPeriod;
        existing.MacdSlowPeriod = StockIndicatorSettings.DefaultMacdSlowPeriod;
        existing.MacdSignalPeriod = StockIndicatorSettings.DefaultMacdSignalPeriod;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }
}
