using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Microsoft.Extensions.Options;
using System.Globalization;

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
    private readonly IIndicatorSettingsService _indicatorSettingsService;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorCalculator indicatorCalculator,
        ISignalAlgorithmRegistry algorithmRegistry,
        ISignalEngine signalEngine,
        IIndicatorSettingsService indicatorSettingsService,
        IOptions<WatchlistOptions> watchlistOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorCalculator = indicatorCalculator;
        _algorithmRegistry = algorithmRegistry;
        _signalEngine = signalEngine;
        _indicatorSettingsService = indicatorSettingsService;
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
        var indicatorSettings = await _indicatorSettingsService.GetOrCreateAsync(trackedAsset.Symbol, cancellationToken);
        var computationSettings = IndicatorComputationSettings.FromEntity(indicatorSettings);
        var computedList = _indicatorCalculator.Compute(orderedBars, computationSettings);
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
        var dailySignals = _signalEngine.Evaluate(trackedAsset.Symbol, orderedBars, computedList);
        var signalsByDate = BuildSignalByDate(dailySignals);
        var historyRows = BuildHistoryRows(visiblePrices, computedByDate, signalsByDate);

        var latestComputed = latestPrice is null ? null : computedList.FirstOrDefault(x => x.Date == latestPrice.Date);
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
            new IndicatorSettingsSnapshot(
                indicatorSettings.ParabolicSarStep,
                indicatorSettings.ParabolicSarMax,
                indicatorSettings.BollingerPeriod,
                indicatorSettings.BollingerStdDev,
                indicatorSettings.MacdFastPeriod,
                indicatorSettings.MacdSlowPeriod,
                indicatorSettings.MacdSignalPeriod,
                indicatorSettings.UpdatedAtUtc),
            orderedBars,
            algorithmType,
            algorithm.DisplayName);
    }

    public async Task SaveIndicatorSettingsAsync(string symbol, IndicatorComputationSettings settings, CancellationToken cancellationToken = default)
    {
        var trackedAsset = ResolveTrackedAsset(symbol);
        var existing = await _indicatorSettingsService.GetOrCreateAsync(trackedAsset.Symbol, cancellationToken);
        existing.ParabolicSarStep = settings.ParabolicSarStep;
        existing.ParabolicSarMax = settings.ParabolicSarMax;
        existing.BollingerPeriod = settings.BollingerPeriod;
        existing.BollingerStdDev = settings.BollingerStdDev;
        existing.MacdFastPeriod = settings.MacdFastPeriod;
        existing.MacdSlowPeriod = settings.MacdSlowPeriod;
        existing.MacdSignalPeriod = settings.MacdSignalPeriod;
        await _indicatorSettingsService.SaveAsync(existing, cancellationToken);
    }

    public async Task ResetIndicatorSettingsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var trackedAsset = ResolveTrackedAsset(symbol);
        await _indicatorSettingsService.ResetToDefaultAsync(trackedAsset.Symbol, cancellationToken);
    }

    private static IReadOnlyList<DashboardHistoryRow> BuildHistoryRows(
        IReadOnlyList<Domain.Entities.DailyPrice> orderedVisiblePrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate,
        IReadOnlyDictionary<DateOnly, TradeSignal> signalsByDate)
    {
        var rows = new List<DashboardHistoryRow>(orderedVisiblePrices.Count);
        decimal? previousSarJumpValue = null;
        decimal? previousMacdDivergence = null;
        var sarWayChangeHistory = new List<decimal?>(orderedVisiblePrices.Count);
        var trendPositionHistory = new List<string?>(orderedVisiblePrices.Count);
        var macdTrendHistory = new List<string?>(orderedVisiblePrices.Count);

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
            sarWayChangeHistory.Add(sarWayChange);
            decimal? sarJumpValue = (previousSar.HasValue && sar.HasValue)
                ? decimal.Round(Math.Abs(sar.Value - previousSar.Value), 4)
                : null;
            var sarNotif = ComputeSarNotification(sarWayChange, i > 0 ? sarWayChangeHistory[i - 1] : null, sarJumpValue, previousSarJumpValue);
            var trendPosition = ComputeTrendPositionOnChange(sarWayChangeHistory, trendPositionHistory, i, sar, price.Close);
            trendPositionHistory.Add(trendPosition);

            var macdDivergence = indicator?.MacdHistogram;
            var macdTrend = ComputeMacdTrend(macdDivergence, previousMacdDivergence);
            macdTrendHistory.Add(macdTrend);
            var currentMacdTrendCount = ComputeMacdTrendCount(macdTrendHistory, i);
            var previousMacdTrendCount = i > 0 ? ComputeMacdTrendCount(macdTrendHistory, i - 1) : 0;
            var macdTrendChange = ComputeMacdTrendChange(currentMacdTrendCount, previousMacdTrendCount);

            signalsByDate.TryGetValue(price.Date, out var signal);

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
                currentMacdTrendCount,
                macdTrendChange == 0 ? null : macdTrendChange.ToString(CultureInfo.InvariantCulture),
                ComputeCountDaysSinceChgVente(trendPositionHistory, i),
                ComputeCountDaysSinceChgAchat(trendPositionHistory, i),
                signal?.Type ?? TradeSignalType.None,
                signal?.SignalReason,
                signal?.SignalReasons ?? Array.Empty<string>(),
                signal?.SignalDirection ?? SignalDirection.None,
                signal?.SignalCategory ?? SignalCategory.None));

            previousSarJumpValue = sarJumpValue;
            previousMacdDivergence = macdDivergence;
        }

        return rows.OrderByDescending(x => x.Date).ToList();
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

    private static string? ComputeTrendPositionOnChange(
        IReadOnlyList<decimal?> sarWayChangeHistory,
        IReadOnlyList<string?> trendPositionHistory,
        int currentIndex,
        decimal? currentSar,
        decimal close)
    {
        if (currentIndex >= 4)
        {
            var currentWay = sarWayChangeHistory[currentIndex];
            var switchedToVente = currentWay < 0m
                && sarWayChangeHistory[currentIndex - 1] > 0m
                && sarWayChangeHistory[currentIndex - 2] > 0m
                && sarWayChangeHistory[currentIndex - 3] > 0m
                && sarWayChangeHistory[currentIndex - 4] > 0m;
            if (switchedToVente)
            {
                return "VENTE";
            }

            var switchedToAchat = currentWay > 0m
                && sarWayChangeHistory[currentIndex - 1] < 0m
                && sarWayChangeHistory[currentIndex - 2] < 0m
                && sarWayChangeHistory[currentIndex - 3] < 0m
                && sarWayChangeHistory[currentIndex - 4] < 0m;
            if (switchedToAchat)
            {
                return "ACHAT";
            }
        }

        if (currentIndex > 0)
        {
            return trendPositionHistory[currentIndex - 1];
        }

        if (!currentSar.HasValue)
        {
            return null;
        }

        return close >= currentSar.Value ? "ACHAT" : "VENTE";
    }

    private static int? ComputeRsiStrengthAbs(decimal? rsi14)
    {
        if (!rsi14.HasValue)
        {
            return null;
        }

        var rsi = rsi14.Value <= 1m ? rsi14.Value * 100m : rsi14.Value;

        if (rsi > 75m)
        {
            return 3;
        }

        if (rsi > 65m)
        {
            return 2;
        }

        if (rsi >= 50m)
        {
            return 1;
        }

        if (rsi < 25m)
        {
            return -3;
        }

        if (rsi < 35m)
        {
            return -2;
        }

        return -1;
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

    private static int ComputeMacdTrendCount(IReadOnlyList<string?> history, int currentIndex)
    {
        var start = Math.Max(0, currentIndex - 3);
        var count = 0;
        for (var i = start; i <= currentIndex; i++)
        {
            count += history[i] switch
            {
                "acc2" => 1,
                "dec2" => -1,
                _ => 0
            };
        }

        return count;
    }

    private static int ComputeMacdTrendChange(int currentMacdTrendCount, int previousMacdTrendCount)
    {
        if (currentMacdTrendCount >= 0 && previousMacdTrendCount <= 0)
        {
            return 1;
        }

        if (currentMacdTrendCount <= 0 && previousMacdTrendCount >= 0)
        {
            return -1;
        }

        return 0;
    }

    private static int ComputeCountDaysSinceChgVente(IReadOnlyList<string?> history, int currentIndex)
        => CountTrendPositionOnSar(history, currentIndex, "VENTE");

    private static int ComputeCountDaysSinceChgAchat(IReadOnlyList<string?> history, int currentIndex)
        => CountTrendPositionOnSar(history, currentIndex, "ACHAT");

    private static int CountTrendPositionOnSar(IReadOnlyList<string?> history, int currentIndex, string expectedTrend, int windowSize = 7)
    {
        var start = Math.Max(0, currentIndex - (windowSize - 1));
        var count = 0;
        for (var i = start; i <= currentIndex; i++)
        {
            if (string.Equals(history[i], expectedTrend, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyDictionary<DateOnly, TradeSignal> BuildSignalByDate(IReadOnlyList<DailySignalResult> dailySignals)
    {
        var signals = new Dictionary<DateOnly, TradeSignal>();

        foreach (var result in dailySignals)
        {
            if (result.ExitSignal is not null)
            {
                signals[result.Date] = result.ExitSignal;
                continue;
            }

            if (result.EntrySignal is not null)
            {
                signals[result.Date] = result.EntrySignal;
            }
        }

        return signals;
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
