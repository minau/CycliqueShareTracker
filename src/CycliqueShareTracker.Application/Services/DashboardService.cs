using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int DefaultHistoryDays = 252;
    private const int IndicatorWarmupDays = 200;
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
        var requiredRows = historyDays + IndicatorWarmupDays;
        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, requiredRows, cancellationToken);
        var ordered = recentPrices.OrderBy(x => x.Date).ToList();
        var visiblePrices = ordered.TakeLast(historyDays).ToList();
        var orderedBars = ordered.Select(x => new PriceBar(x.Date, x.Open, x.High, x.Low, x.Close, x.Volume)).ToList();
        var computedList = _indicatorCalculator.Compute(orderedBars);
        var computedByDate = computedList.ToDictionary(x => x.Date);
        var algorithm = _algorithmRegistry.Get(algorithmType);

        decimal? dayChange = null;
        var orderedDesc = ordered.OrderByDescending(x => x.Date).ToList();
        if (orderedDesc.Count >= 2 && orderedDesc[1].Close != 0)
        {
            dayChange = ((orderedDesc[0].Close / orderedDesc[1].Close) - 1m) * 100m;
        }

        var chartPoints = visiblePrices.Select(price =>
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
        var historyRows = BuildHistoryRows(visiblePrices, computedByDate);

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
            historyRows,
            markers,
            currentPosition,
            orderedBars,
            algorithmType,
            algorithm.DisplayName);
    }

    private static IReadOnlyList<DashboardHistoryRow> BuildHistoryRows(
        IReadOnlyList<Domain.Entities.DailyPrice> orderedVisiblePrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate)
    {
        var rows = new List<DashboardHistoryRow>(orderedVisiblePrices.Count);
        string? previousTrendPosition = null;
        decimal? previousSarWayChange = null;
        decimal? previousSarJumpValue = null;
        decimal? previousMacdDivergence = null;
        string? previousMacdTrend = null;
        var currentMacdTrendCount = 0;
        int? lastVenteChangeIndex = null;
        int? lastAchatChangeIndex = null;

        for (var i = 0; i < orderedVisiblePrices.Count; i++)
        {
            var price = orderedVisiblePrices[i];
            computedByDate.TryGetValue(price.Date, out var indicator);

            ComputedIndicator? previousIndicator = null;
            if (i > 0)
            {
                computedByDate.TryGetValue(orderedVisiblePrices[i - 1].Date, out previousIndicator);
            }

            var previousSar = previousIndicator?.ParabolicSar;
            var sar = indicator?.ParabolicSar;
            var sarWayChange = ComputeSarWayChange(previousSar, sar);
            var sarJumpValue = (previousSar.HasValue && sar.HasValue)
                ? decimal.Round(Math.Abs(sar.Value - previousSar.Value), 4)
                : null;
            var sarNotif = ComputeSarNotification(sarWayChange, previousSarWayChange, sarJumpValue, previousSarJumpValue);
            var trendPosition = ComputeTrendPositionOnSar(previousTrendPosition, price, i > 0 ? orderedVisiblePrices[i - 1] : null, sar, previousSar);
            if (trendPosition == "VENTE" && trendPosition != previousTrendPosition)
            {
                lastVenteChangeIndex = i;
            }
            else if (trendPosition == "ACHAT" && trendPosition != previousTrendPosition)
            {
                lastAchatChangeIndex = i;
            }

            var macdDivergence = indicator?.MacdHistogram;
            var macdTrend = ComputeMacdTrend(macdDivergence, previousMacdDivergence);
            var macdTrendChanged = !string.IsNullOrWhiteSpace(macdTrend)
                && !string.IsNullOrWhiteSpace(previousMacdTrend)
                && !string.Equals(macdTrend, previousMacdTrend, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(macdTrend))
            {
                currentMacdTrendCount = string.Equals(macdTrend, previousMacdTrend, StringComparison.Ordinal)
                    ? currentMacdTrendCount + 1
                    : 1;
            }
            else
            {
                currentMacdTrendCount = 0;
            }

            rows.Add(new DashboardHistoryRow(
                price.Date,
                price.Open,
                price.High,
                price.Low,
                price.Close,
                sar,
                indicator?.MacdSignalLine,
                indicator?.MacdLine,
                macdDivergence,
                indicator?.Rsi14,
                indicator?.BollingerUpper,
                indicator?.BollingerMiddle,
                indicator?.BollingerLower,
                sarWayChange,
                sarJumpValue,
                sarNotif,
                trendPosition,
                ComputeRsiStrengthAbs(indicator?.Rsi14),
                ComputeBollingerBottomUpSignal(price, indicator),
                ComputeBollingerMiddleHitUp(price, i > 0 ? orderedVisiblePrices[i - 1] : null, indicator, previousIndicator),
                ComputeBollingerMiddleHitDown(price, i > 0 ? orderedVisiblePrices[i - 1] : null, indicator, previousIndicator),
                macdDivergence.HasValue ? (macdDivergence.Value > 0 ? 1 : -1) : null,
                macdTrend,
                currentMacdTrendCount > 0 ? currentMacdTrendCount : null,
                macdTrendChanged ? "chg" : null,
                lastVenteChangeIndex.HasValue ? i - lastVenteChangeIndex.Value : null,
                lastAchatChangeIndex.HasValue ? i - lastAchatChangeIndex.Value : null));

            previousSarWayChange = sarWayChange;
            previousSarJumpValue = sarJumpValue;
            previousTrendPosition = trendPosition;
            previousMacdDivergence = macdDivergence;
            previousMacdTrend = macdTrend;
        }

        return rows;
    }

    private static decimal? ComputeSarWayChange(decimal? previousSar, decimal? currentSar)
    {
        if (!previousSar.HasValue || !currentSar.HasValue || previousSar <= 0 || currentSar <= 0)
        {
            return null;
        }

        return decimal.Round((decimal)Math.Log((double)(currentSar.Value / previousSar.Value)), 6);
    }

    private static string? ComputeSarNotification(decimal? sarWayChange, decimal? previousSarWayChange, decimal? sarJumpValue, decimal? previousSarJumpValue)
    {
        if (!sarWayChange.HasValue)
        {
            return null;
        }

        if (previousSarWayChange.HasValue)
        {
            var signChanged = (sarWayChange.Value > 0 && previousSarWayChange.Value < 0)
                || (sarWayChange.Value < 0 && previousSarWayChange.Value > 0);
            if (signChanged)
            {
                return "chg";
            }
        }

        if (!sarJumpValue.HasValue || !previousSarJumpValue.HasValue)
        {
            return null;
        }

        return sarJumpValue.Value >= previousSarJumpValue.Value ? "acc" : "dec";
    }

    private static string? ComputeTrendPositionOnSar(
        string? previousTrendPosition,
        Domain.Entities.DailyPrice currentPrice,
        Domain.Entities.DailyPrice? previousPrice,
        decimal? currentSar,
        decimal? previousSar)
    {
        if (currentSar.HasValue && previousSar.HasValue && previousPrice is not null)
        {
            var switchedToVente = previousPrice.Close >= previousSar.Value && currentPrice.Close < currentSar.Value;
            if (switchedToVente)
            {
                return "VENTE";
            }

            var switchedToAchat = previousPrice.Close <= previousSar.Value && currentPrice.Close > currentSar.Value;
            if (switchedToAchat)
            {
                return "ACHAT";
            }
        }

        if (!string.IsNullOrWhiteSpace(previousTrendPosition))
        {
            return previousTrendPosition;
        }

        if (!currentSar.HasValue)
        {
            return null;
        }

        return currentPrice.Close >= currentSar.Value ? "ACHAT" : "VENTE";
    }

    private static int? ComputeRsiStrengthAbs(decimal? rsi)
    {
        if (!rsi.HasValue)
        {
            return null;
        }

        if (rsi.Value >= 70m || rsi.Value <= 30m)
        {
            return 3;
        }

        if (rsi.Value >= 60m || rsi.Value <= 40m)
        {
            return 2;
        }

        return 1;
    }

    private static string? ComputeBollingerBottomUpSignal(Domain.Entities.DailyPrice price, ComputedIndicator? indicator)
    {
        if (indicator?.BollingerLower is null || indicator.BollingerUpper is null)
        {
            return null;
        }

        if (price.Close <= indicator.BollingerLower.Value)
        {
            return "ACHAT";
        }

        if (price.Close >= indicator.BollingerUpper.Value)
        {
            return "VENTE";
        }

        return null;
    }

    private static string? ComputeBollingerMiddleHitUp(
        Domain.Entities.DailyPrice currentPrice,
        Domain.Entities.DailyPrice? previousPrice,
        ComputedIndicator? indicator,
        ComputedIndicator? previousIndicator)
    {
        if (previousPrice is null || indicator?.BollingerMiddle is null || previousIndicator?.BollingerMiddle is null)
        {
            return null;
        }

        var crossedUp = previousPrice.Close <= previousIndicator.BollingerMiddle.Value
            && currentPrice.Close > indicator.BollingerMiddle.Value;
        return crossedUp ? "hit" : null;
    }

    private static string? ComputeBollingerMiddleHitDown(
        Domain.Entities.DailyPrice currentPrice,
        Domain.Entities.DailyPrice? previousPrice,
        ComputedIndicator? indicator,
        ComputedIndicator? previousIndicator)
    {
        if (previousPrice is null || indicator?.BollingerMiddle is null || previousIndicator?.BollingerMiddle is null)
        {
            return null;
        }

        var crossedDown = previousPrice.Close >= previousIndicator.BollingerMiddle.Value
            && currentPrice.Close < indicator.BollingerMiddle.Value;
        return crossedDown ? "hit" : null;
    }

    private static string? ComputeMacdTrend(decimal? macdDivergence, decimal? previousMacdDivergence)
    {
        if (!macdDivergence.HasValue || !previousMacdDivergence.HasValue)
        {
            return null;
        }

        return macdDivergence.Value >= previousMacdDivergence.Value ? "acc2" : "dec2";
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

            var signals = new[] { result.EntrySignal, result.ExitSignal }.Where(x => x is not null).Cast<TradeSignal>();
            foreach (var signal in signals)
            {
                var verticalPrice = signal.Type is TradeSignalType.Long or TradeSignalType.LeaveShort
                    ? point.Low
                    : point.High;
                var actionText = signal.IsEntry ? "Signal entrée" : "Signal sortie";

                markers.Add(new TradeMarker(
                    result.Date,
                    signal.Type,
                    verticalPrice,
                    signal.Reason,
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
