using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int DefaultHistoryDays = 252;
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly ISignalAlgorithmRegistry _algorithmRegistry;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        ISignalRepository signalRepository,
        ISignalService signalService,
        IExitSignalService exitSignalService,
        IIndicatorCalculator indicatorCalculator,
        ISignalAlgorithmRegistry algorithmRegistry,
        IOptions<WatchlistOptions> watchlistOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorCalculator = indicatorCalculator;
        _algorithmRegistry = algorithmRegistry;
        _watchlist = BuildWatchlist(watchlistOptions.Value.Assets);
        _dashboardOptions = dashboardOptions.Value;
    }

    public IReadOnlyList<TrackedAssetOptions> GetTrackedAssets() => _watchlist;

    public async Task<IReadOnlyList<AssetSnapshotResult>> GetWatchlistSnapshotsAsync(AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        var results = new List<AssetSnapshotResult>(_watchlist.Count);

        foreach (var trackedAsset in _watchlist)
        {
            try
            {
                var snapshot = await GetSnapshotAsync(trackedAsset.Symbol, algorithmType, cancellationToken);
                results.Add(new AssetSnapshotResult(trackedAsset, snapshot, null));
            }
            catch (Exception ex)
            {
                results.Add(new AssetSnapshotResult(trackedAsset, null, ex.Message));
            }
        }

        return results;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(string symbol, AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        var trackedAsset = ResolveTrackedAsset(symbol);
        var asset = await _assetRepository.GetOrCreateAsync(trackedAsset.Symbol, trackedAsset.Name, trackedAsset.Market, cancellationToken);
        var latestPrice = await _priceRepository.GetLatestAsync(asset.Id, cancellationToken);
        var historyDays = _dashboardOptions.HistoryDays > 0 ? _dashboardOptions.HistoryDays : DefaultHistoryDays;
        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, historyDays, cancellationToken);
        var ordered = recentPrices.OrderBy(x => x.Date).ToList();
        var orderedBars = ordered.Select(x => new PriceBar(x.Date, x.Open, x.High, x.Low, x.Close, x.Volume)).ToList();
        var computedList = _indicatorCalculator.Compute(orderedBars);
        var computedByDate = computedList.ToDictionary(x => x.Date);
        var algorithm = _algorithmRegistry.Get(algorithmType);
        var algorithmResult = algorithm.ComputeSignals(orderedBars, new AlgorithmContext(computedList));
        var pointsByDate = algorithmResult.Points.ToDictionary(p => p.Date);

        decimal? dayChange = null;
        var orderedDesc = recentPrices.OrderByDescending(x => x.Date).ToList();
        if (orderedDesc.Count >= 2 && orderedDesc[1].Close != 0)
        {
            dayChange = ((orderedDesc[0].Close / orderedDesc[1].Close) - 1m) * 100m;
        }

        var chartPoints = ordered.Select(price =>
        {
            computedByDate.TryGetValue(price.Date, out var indicator);
            pointsByDate.TryGetValue(price.Date, out var point);
            point ??= new AlgorithmSignalPoint(price.Date, false, false, false, false, 0, 0, null, "N/A", "N/A", Array.Empty<ScoreFactorDetail>(), Array.Empty<ScoreFactorDetail>());

            return new DashboardChartPoint(
                price.Date,
                price.Open,
                price.High,
                price.Low,
                price.Close,
                indicator?.Sma50,
                indicator?.Sma200,
                indicator?.Rsi14,
                indicator?.MacdLine,
                indicator?.MacdSignalLine,
                indicator?.MacdHistogram,
                indicator?.BollingerMiddle,
                indicator?.BollingerUpper,
                indicator?.BollingerLower,
                indicator?.ParabolicSar,
                point.BuyScore,
                point.BuyReason,
                point.BuyDetails,
                point.IsBuyZone ? SignalLabel.BuyZone : SignalLabel.Watch,
                point.SellScore,
                point.SellReason,
                point.SellDetails,
                point.IsSellZone ? ExitSignalLabel.SellZone : ExitSignalLabel.Hold,
                point.IsBuyZone,
                point.IsSellZone,
                point.BuySignal,
                point.SellSignal);
        }).ToList();

        AlgorithmSignalPoint? latestSignalPoint = null;
        if (latestPrice is not null)
        {
            pointsByDate.TryGetValue(latestPrice.Date, out latestSignalPoint);
        }
        var latestComputed = latestPrice is null ? null : computedList.FirstOrDefault(x => x.Date == latestPrice.Date);

        return new DashboardSnapshot(
            asset.Symbol,
            asset.Name,
            latestPrice?.Date,
            latestPrice?.Close,
            dayChange,
            latestComputed?.Sma50,
            latestComputed?.Sma200,
            latestComputed?.Ema12,
            latestComputed?.Ema26,
            latestComputed?.Rsi14,
            latestComputed?.Drawdown52WeeksPercent,
            latestComputed?.MacdLine,
            latestComputed?.MacdSignalLine,
            latestComputed?.MacdHistogram,
            latestSignalPoint?.BuyScore,
            latestSignalPoint?.IsBuyZone == true ? SignalLabel.BuyZone : SignalLabel.Watch,
            latestSignalPoint?.BuyReason,
            latestSignalPoint?.BuyDetails ?? Array.Empty<ScoreFactorDetail>(),
            latestSignalPoint?.SellScore,
            latestSignalPoint?.IsSellZone == true ? ExitSignalLabel.SellZone : ExitSignalLabel.Hold,
            latestSignalPoint?.SellReason,
            latestSignalPoint?.SellDetails ?? Array.Empty<ScoreFactorDetail>(),
            chartPoints,
            orderedBars,
            algorithmType,
            algorithm.DisplayName);
    }

    public async Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(string symbol, AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(symbol, algorithmType, cancellationToken);

        return snapshot.ChartPoints
            .OrderByDescending(x => x.Date)
            .Select(x => new SignalHistoryRow(
                x.Date,
                x.Close,
                x.Sma50,
                x.Sma200,
                x.Rsi14,
                null,
                x.EntryScore,
                x.SignalLabel,
                x.EntryPrimaryReason,
                x.EntryScoreFactors,
                x.ExitScore,
                x.ExitSignalLabel,
                x.ExitPrimaryReason,
                x.ExitScoreFactors))
            .ToList();
    }

    private TrackedAssetOptions ResolveTrackedAsset(string symbol)
    {
        if (_watchlist.Count == 0)
        {
            throw new InvalidOperationException("Watchlist configuration is empty.");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return _watchlist[0];
        }

        return _watchlist.FirstOrDefault(asset => string.Equals(asset.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Symbol '{symbol}' is not configured in the watchlist.");
    }

    private static IReadOnlyList<TrackedAssetOptions> BuildWatchlist(IReadOnlyList<TrackedAssetOptions>? configuredAssets)
    {
        var source = configuredAssets is { Count: > 0 }
            ? configuredAssets
            : WatchlistOptions.DefaultAssets;

        return source
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Symbol))
            .GroupBy(asset => asset.Symbol.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

}
