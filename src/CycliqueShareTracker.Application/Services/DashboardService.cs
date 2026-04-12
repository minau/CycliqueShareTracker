using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int DefaultHistoryDays = 252;
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly ISignalAlgorithmRegistry _algorithmRegistry;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ISignalEngine _signalEngine;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorCalculator indicatorCalculator,
        ISignalAlgorithmRegistry algorithmRegistry,
        ISignalEngine signalEngine,
        IOptions<WatchlistOptions> watchlistOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorCalculator = indicatorCalculator;
        _algorithmRegistry = algorithmRegistry;
        _signalEngine = signalEngine;
        _watchlist = WatchlistOptions.BuildTrackedAssets(watchlistOptions.Value.Assets);
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

        decimal? dayChange = null;
        var orderedDesc = recentPrices.OrderByDescending(x => x.Date).ToList();
        if (orderedDesc.Count >= 2 && orderedDesc[1].Close != 0)
        {
            dayChange = ((orderedDesc[0].Close / orderedDesc[1].Close) - 1m) * 100m;
        }

        var chartPoints = ordered.Select(price =>
        {
            computedByDate.TryGetValue(price.Date, out var indicator);

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
                indicator?.ParabolicSar);
        }).ToList();

        var latestComputed = latestPrice is null ? null : computedList.FirstOrDefault(x => x.Date == latestPrice.Date);
        var dailySignals = _signalEngine.Evaluate(trackedAsset.Symbol, orderedBars, computedList);
        var markers = BuildTradeMarkers(dailySignals, chartPoints);
        var currentPosition = dailySignals.LastOrDefault()?.PositionAfter ?? TrackedPosition.Empty(trackedAsset.Symbol);

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
            chartPoints,
            markers,
            currentPosition,
            orderedBars,
            algorithmType,
            algorithm.DisplayName);
    }

    private static IReadOnlyList<TradeMarker> BuildTradeMarkers(IReadOnlyList<DailySignalResult> dailySignals, IReadOnlyList<DashboardChartPoint> chartPoints)
    {
        var priceByDate = chartPoints.ToDictionary(x => x.Date, x => x);
        var markers = new List<TradeMarker>();

        foreach (var result in dailySignals)
        {
            if (!priceByDate.TryGetValue(result.Date, out var point))
            {
                continue;
            }

            foreach (var action in result.Actions.Where(x => x.Status is TradeExecutionStatus.Executed or TradeExecutionStatus.PendingWindow))
            {
                var verticalPrice = action.SignalType is TradeSignalType.Long or TradeSignalType.LeaveShort
                    ? point.Low
                    : point.High;
                var actionText = action.Status == TradeExecutionStatus.PendingWindow
                    ? $"{action.ActionType} (pending fenêtre 18:04-18:20)"
                    : action.ActionType.ToString();

                markers.Add(new TradeMarker(
                    result.Date,
                    action.SignalType,
                    verticalPrice,
                    action.Reason,
                    actionText,
                    FormatPosition(result.PositionAfter)));
            }
        }

        return markers;
    }

    private static string FormatPosition(TrackedPosition position)
        => position.Side switch
        {
            PositionSide.Long => $"{position.Quantity:0} LONG {position.Product} - {position.EntryDate:dd/MM/yyyy}",
            PositionSide.Short => $"{position.Quantity:0} SHORT {position.Product} - {position.EntryDate:dd/MM/yyyy}",
            _ => "Aucune position"
        };

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
}
