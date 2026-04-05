using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class BacktestService : IBacktestService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IBacktestEngine _backtestEngine;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;

    public BacktestService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IBacktestEngine backtestEngine,
        IOptions<WatchlistOptions> watchlistOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _backtestEngine = backtestEngine;
        _watchlist = watchlistOptions.Value.Assets is { Count: > 0 }
            ? watchlistOptions.Value.Assets
            : WatchlistOptions.DefaultAssets.ToList();
    }

    public async Task<BacktestResult> RunAsync(BacktestRequest request, CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate)
        {
            throw new InvalidOperationException("La date de début doit être inférieure ou égale à la date de fin.");
        }

        var normalizedSymbols = request.Symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedSymbols.Count == 0)
        {
            throw new InvalidOperationException("Aucun symbole fourni pour le backtest.");
        }

        var config = request.StrategyConfig ?? StrategyConfig.Default;
        var results = new List<BacktestAssetResult>();

        foreach (var symbol in normalizedSymbols)
        {
            var tracked = _watchlist.FirstOrDefault(w => string.Equals(w.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            var assetName = tracked?.Name ?? symbol;
            var market = tracked?.Market ?? string.Empty;

            var asset = await _assetRepository.GetOrCreateAsync(symbol, assetName, market, cancellationToken);
            var prices = await _priceRepository.GetPricesInRangeAsync(asset.Id, request.StartDate, request.EndDate, cancellationToken);
            var bars = prices.Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList();

            results.Add(_backtestEngine.RunForAsset(symbol, assetName, bars, request.IncludeMacdInScoring, config));
        }

        var aggregate = BuildAggregateMetrics(results);
        return new BacktestResult(request, aggregate, results);
    }

    private static BacktestMetrics BuildAggregateMetrics(IReadOnlyList<BacktestAssetResult> assetResults)
    {
        var allTrades = assetResults.SelectMany(x => x.Trades).ToList();

        if (allTrades.Count == 0)
        {
            return new BacktestMetrics(0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        var totalTrades = allTrades.Count;
        var wins = allTrades.Count(t => t.PerformancePercent > 0m);
        var losses = allTrades.Count(t => t.PerformancePercent < 0m);

        var gains = allTrades.Where(t => t.PerformancePercent > 0m).Select(t => t.PerformancePercent).ToList();
        var lossValues = allTrades.Where(t => t.PerformancePercent < 0m).Select(t => Math.Abs(t.PerformancePercent)).ToList();

        var grossGain = gains.Sum();
        var grossLoss = lossValues.Sum();

        var averageCapital = assetResults
            .Select(a => a.Trades.Aggregate(1m, (capital, trade) => capital * (1m + trade.PerformancePercent / 100m)))
            .Average();

        return new BacktestMetrics(
            totalTrades,
            wins,
            losses,
            decimal.Round((decimal)wins / totalTrades * 100m, 2),
            decimal.Round(gains.Count == 0 ? 0m : gains.Average(), 2),
            decimal.Round(lossValues.Count == 0 ? 0m : lossValues.Average(), 2),
            decimal.Round(grossLoss == 0m ? grossGain : grossGain / grossLoss, 2),
            decimal.Round((averageCapital - 1m) * 100m, 2),
            decimal.Round(assetResults.Max(a => a.Metrics.MaxDrawdownPercent), 2),
            decimal.Round((decimal)allTrades.Average(t => t.DurationDays), 2));
    }
}
